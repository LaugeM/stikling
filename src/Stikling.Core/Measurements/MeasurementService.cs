using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Measurements;

/// <summary>One value to write down: a built-in kind or an own kind, and the number. A length is in cm.</summary>
public sealed record MeasureValue(MeasureKind? Kind, Guid? OwnKindId, decimal Value)
{
    public static MeasureValue Of(MeasureKind kind, decimal value) => new(kind, null, value);

    public static MeasureValue OfOwn(Guid ownKindId, decimal value) => new(null, ownKindId, value);
}

/// <summary>One point on a growth curve: the day it was measured, the value, and the history entry it belongs to.</summary>
public sealed record CurvePoint(DateOnly Day, decimal Value, Guid EntryId);

/// <summary>The measurements of one kind over time, oldest first.</summary>
public sealed record GrowthCurve(MeasureKind? Kind, Guid? OwnKindId, string Name, bool IsLength, IReadOnlyList<CurvePoint> Points)
{
    public CurvePoint Latest => Points[^1];
}

/// <summary>
/// Writing down measurements with a history entry, the growth curves drawn from them, and the kinds
/// people add themselves. Each value is a record of its own, and an entry has at most one for each kind.
/// </summary>
public sealed class MeasurementService(
    IMeasurementRepository measurements, IOwnMeasureKindRepository ownKinds, ITimelineRepository timeline, TimeProvider time)
{
    /// <summary>The fewest points a curve is drawn from.</summary>
    public const int CurveMinimum = 3;

    // ---- the values of an entry ----

    /// <summary>
    /// Makes the entry hold exactly these values: new ones are added, changed ones updated and the rest
    /// taken off (soft delete). An entry of only measurements has its line written again. Throws
    /// <see cref="InvalidOperationException"/> with the reason, before anything is saved, when a value isn't valid.
    /// </summary>
    public async Task SetForEntryAsync(TimelineEntry entry, IReadOnlyList<MeasureValue> values)
    {
        var plan = await PlanAsync(entry, values);
        if (MeasuredEntries.IsMeasured(entry))
        {
            MeasuredEntries.Apply(entry, plan.Final);
            await timeline.UpdateAsync(entry);
        }
        await CommitAsync(plan);
    }

    /// <summary>Throws the same way <see cref="SetForEntryAsync"/> would, without saving anything, so a note isn't saved with measurements that then fail. For an entry that is not saved yet.</summary>
    public async Task CheckAsync(SubjectType subjectType, Guid subjectId, IReadOnlyList<MeasureValue> values) =>
        await PlanAsync(new TimelineEntry { SubjectType = subjectType, SubjectId = subjectId, OccurredAt = time.GetLocalNow() }, values);

    /// <summary>The same for an entry that is saved, so the measurements it already has (also of a kind that was removed) are found.</summary>
    public async Task CheckAsync(TimelineEntry entry, IReadOnlyList<MeasureValue> values) =>
        await PlanAsync(entry, values);

    /// <summary>
    /// Adds an entry for measurements taken with no note and no photos, dated <paramref name="day"/> or now.
    /// It is a change entry with the event Measured, so older versions still load it.
    /// </summary>
    public async Task<TimelineEntry> AddMeasuredEntryAsync(
        SubjectType subjectType, Guid subjectId, IReadOnlyList<MeasureValue> values, DateOnly? day = null)
    {
        var entry = new TimelineEntry
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.Measured,
            OccurredAt = day is { } d ? time.MomentOn(d) : time.GetLocalNow()
        };
        var plan = await PlanAsync(entry, values);
        MeasuredEntries.Apply(entry, plan.Final);
        await timeline.AddAsync(entry);
        await CommitAsync(plan);
        return entry;
    }

    /// <summary>The live measurements of a plant or propagation, whose entries are still on its history. Ones no kind can be read from are left out.</summary>
    public async Task<IReadOnlyList<Measurement>> ForSubjectAsync(Guid subjectId)
    {
        var live = (await timeline.GetForAsync(subjectId)).Where(e => !e.IsDeleted).Select(e => e.Id).ToHashSet();
        return [.. (await measurements.GetAllAsync()).Where(m => m.SubjectId == subjectId && m.HasKind && live.Contains(m.EntryId))];
    }

    /// <summary>The live measurements written with the given entries, by entry id, one for each kind (the newest if a merge made two).</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Measurement>>> ForEntriesAsync(IEnumerable<Guid> entryIds)
    {
        var ids = entryIds.ToHashSet();
        return OnePerKind((await measurements.GetAllAsync()).Where(m => ids.Contains(m.EntryId) && m.HasKind))
            .GroupBy(m => m.EntryId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Measurement>)MeasureText.InOrder(g));
    }

    /// <summary>The growth curves of a plant or propagation, see <see cref="Curves"/>.</summary>
    /// <summary>
    /// A plant potted up from a propagation also gets that propagation's measurements taken up to the
    /// day it was potted up, so the kinds they share make one curve.
    /// </summary>
    public async Task<IReadOnlyList<GrowthCurve>> CurvesForAsync(Guid subjectId, Guid? earlierSubjectId = null, DateOnly? earlierUntil = null)
    {
        var own = (await ownKinds.GetAllAsync()).ToDictionary(k => k.Id);
        var all = (await ForSubjectAsync(subjectId)).ToList();
        if (earlierSubjectId is { } earlierId && earlierUntil is { } until)
            all.AddRange((await ForSubjectAsync(earlierId)).Where(m => time.LocalDay(m.MeasuredAt) <= until));
        return Curves(all, time.LocalDay, own);
    }

    /// <summary>
    /// One curve for each kind with <see cref="CurveMinimum"/> or more points, one point for each entry
    /// (the newest if a merge made two for the same entry and kind), by the day of the measurement.
    /// Built-in kinds come first in their usual order, then own kinds by name. An own kind that is still
    /// there goes by its current name, a removed one by the name its measurements kept.
    /// </summary>
    public static IReadOnlyList<GrowthCurve> Curves(
        IEnumerable<Measurement> all, Func<DateTimeOffset, DateOnly> localDay, IReadOnlyDictionary<Guid, OwnMeasureKind>? own = null)
    {
        var curves = new List<GrowthCurve>();
        foreach (var group in OnePerKind(all.Where(m => m.HasKind && !m.IsDeleted)).GroupBy(KeyOf))
        {
            var points = group
                .OrderBy(m => m.MeasuredAt).ThenBy(m => m.CreatedAt)
                .Select(m => new CurvePoint(localDay(m.MeasuredAt), m.Value, m.EntryId))
                .ToList();
            if (points.Count < CurveMinimum)
                continue;

            var first = group.First();
            if (first.Kind is { } kind)
            {
                curves.Add(new GrowthCurve(kind, null, MeasureText.KindName(kind), MeasureKinds.IsLength(kind), points));
                continue;
            }

            var current = own is not null && own.TryGetValue(first.OwnKindId!.Value, out var kindNow) && !kindNow.IsDeleted ? kindNow : null;
            var newest = group.MaxBy(m => m.MeasuredAt)!;
            curves.Add(new GrowthCurve(
                null, first.OwnKindId, current?.Name ?? newest.OwnKindName ?? "", current?.IsLength ?? newest.IsLength, points));
        }

        return [.. curves
            .OrderBy(c => c.Kind is { } k ? (int)k : int.MaxValue)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Takes the entry's measurements off together with it (soft delete).</summary>
    public async Task RemoveForEntryAsync(Guid entryId)
    {
        foreach (var m in (await measurements.GetAllAsync()).Where(m => m.EntryId == entryId))
            await measurements.DeleteAsync(m.Id);
    }

    // ---- own kinds ----

    /// <summary>The own kinds that aren't deleted, by name.</summary>
    public async Task<IReadOnlyList<OwnMeasureKind>> GetOwnKindsAsync() =>
        [.. (await ownKinds.GetAllAsync()).OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Adds an own kind, or returns the one that is already there. The same name twice is one kind. A
    /// removed kind with that name comes back. The id comes from the name, so two devices adding
    /// "Nodes" make the same record, unless a kind was renamed and still holds that id, then the new
    /// one gets an id of its own.
    /// </summary>
    public async Task<OwnMeasureKind> AddOwnKindAsync(string name, bool isLength)
    {
        var trimmed = (name ?? "").Trim();
        var kind = new OwnMeasureKind { Name = trimmed, IsLength = isLength };
        if (kind.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        if (IsBuiltInName(trimmed))
            throw new InvalidOperationException(F(Strings.Measures.KindNameBuiltIn, trimmed));

        var stored = await ownKinds.GetStoredAsync();
        if (stored.FirstOrDefault(k => !k.IsDeleted && SameName(k.Name, trimmed)) is { } existing)
            return existing;

        var id = OwnMeasureKind.IdFor(trimmed);
        var holder = stored.FirstOrDefault(k => k.Id == id);
        if (holder is { IsDeleted: true })
        {
            holder.DeletedAt = null;
            holder.Name = trimmed;
            holder.IsLength = isLength;
            await ownKinds.SaveAsync(holder);
            return holder;
        }

        // A live kind holding the id has been renamed since, so this one can't share it
        kind.Id = holder is null ? id : Guid.NewGuid();
        await ownKinds.SaveAsync(kind);
        return kind;
    }

    /// <summary>Renames a kind. It keeps its id. Measurements already written keep the name they have until they are saved again.</summary>
    public async Task RenameOwnKindAsync(Guid id, string name)
    {
        var kind = await ownKinds.GetAsync(id) ?? throw new InvalidOperationException(Strings.Measures.KindGone);
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException(Strings.Measures.KindNeedsName);
        if (kind.Name == trimmed)
            return;
        if (IsBuiltInName(trimmed))
            throw new InvalidOperationException(F(Strings.Measures.KindNameBuiltIn, trimmed));
        if ((await ownKinds.GetAllAsync()).Any(k => k.Id != id && SameName(k.Name, trimmed)))
            throw new InvalidOperationException(F(Strings.Measures.KindNameTaken, trimmed));

        kind.Name = trimmed;
        await ownKinds.SaveAsync(kind);
    }

    /// <summary>How many live measurements were written with this kind.</summary>
    public async Task<int> OwnKindUseAsync(Guid id) =>
        (await measurements.GetAllAsync()).Count(m => m.OwnKindId == id);

    /// <summary>Removes a kind. Measurements already written are left alone and show the name they were written with.</summary>
    public Task RemoveOwnKindAsync(Guid id) => ownKinds.DeleteAsync(id);

    // ---- working it out ----

    private sealed record Plan(
        TimelineEntry Entry, List<Measurement> Final, List<Measurement> ToSave, List<Measurement> ToDelete);

    private async Task<Plan> PlanAsync(TimelineEntry entry, IReadOnlyList<MeasureValue> values)
    {
        if (entry.SubjectType is not (SubjectType.Plant or SubjectType.Propagation))
            throw new InvalidOperationException(Strings.Measures.NeedsSubject);

        // The last value given for a kind counts
        var wanted = new Dictionary<string, MeasureValue>();
        foreach (var value in values)
            wanted[KeyOf(value)] = value;
        if (wanted.Count == 0 && MeasuredEntries.IsMeasured(entry))
            throw new InvalidOperationException(Strings.Measures.EntryNeedsValue);

        var stored = (await measurements.GetAllAsync()).Where(m => m.EntryId == entry.Id && m.HasKind).ToList();
        var kinds = (await ownKinds.GetStoredAsync()).ToDictionary(k => k.Id);
        var plan = new Plan(entry, [], [], []);

        foreach (var (key, value) in wanted)
        {
            // The newest one is kept, any other for the same kind is taken off
            var same = stored.Where(m => KeyOf(m) == key).OrderByDescending(m => m.UpdatedAt).ToList();
            var current = same.FirstOrDefault();
            plan.ToDelete.AddRange(same.Skip(1));

            var draft = current?.Copy() ?? new Measurement
            {
                SubjectType = entry.SubjectType,
                SubjectId = entry.SubjectId,
                EntryId = entry.Id
            };
            draft.MeasuredAt = entry.OccurredAt;
            draft.Kind = value.Kind;
            draft.OwnKindId = value.OwnKindId;

            if (value.OwnKindId is { } ownId)
            {
                if (kinds.TryGetValue(ownId, out var kind) && !kind.IsDeleted)
                {
                    draft.OwnKindName = kind.Name;
                    draft.OwnKindIsLength = kind.IsLength;
                }
                else if (current is null)
                {
                    throw new InvalidOperationException(Strings.Measures.KindGone);
                }
                // A removed kind keeps the name and unit this measurement already has
            }
            else
            {
                draft.OwnKindName = null;
                draft.OwnKindIsLength = null;
            }

            draft.Value = draft.IsLength
                ? Math.Round(value.Value, 1, MidpointRounding.AwayFromZero)
                : value.Value;
            if (draft.Validate().FirstOrDefault() is { } error)
                throw new InvalidOperationException(error);

            plan.Final.Add(draft);
            if (current is null || !Same(current, draft))
                plan.ToSave.Add(draft);
        }

        plan.ToDelete.AddRange(stored.Where(m => !wanted.ContainsKey(KeyOf(m))));
        return plan;
    }

    private async Task CommitAsync(Plan plan)
    {
        foreach (var m in plan.ToSave)
            await measurements.SaveAsync(m);
        foreach (var m in plan.ToDelete)
            await measurements.DeleteAsync(m.Id);
    }

    private static bool Same(Measurement a, Measurement b) =>
        a.Value == b.Value && a.MeasuredAt == b.MeasuredAt && a.OwnKindName == b.OwnKindName && a.OwnKindIsLength == b.OwnKindIsLength;

    // If a merge made two for the same entry and kind, the one with the newest UpdatedAt counts
    private static IEnumerable<Measurement> OnePerKind(IEnumerable<Measurement> all) =>
        all.GroupBy(m => (m.EntryId, Kind: KeyOf(m))).Select(g => g.MaxBy(m => m.UpdatedAt)!);

    private static string KeyOf(Measurement m) => m.Kind is { } k ? k.ToString() : m.OwnKindId.ToString()!;

    private static string KeyOf(MeasureValue v) => v.Kind is { } k ? k.ToString() : v.OwnKindId.ToString()!;

    private static bool IsBuiltInName(string name) =>
        MeasureText.BuiltIn.Any(k => SameName(MeasureText.KindName(k), name) || SameName(k.ToString(), name));

    private static bool SameName(string a, string b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
