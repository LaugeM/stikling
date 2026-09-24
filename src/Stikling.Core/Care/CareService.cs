using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Care;

/// <summary>
/// Writing to the care log. The entry is always saved; the plant's history only gets a line
/// for the notable kinds, so a weekly watering doesn't bury the photos and notes.
/// </summary>
public sealed class CareService(ICareLogRepository logs, ITimelineRepository timeline, TimeProvider time)
{
    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>A blank entry for one plant, dated today.</summary>
    public CareLog Start(Guid plantId, CareKind kind = CareKind.Watered) =>
        new() { PlantId = plantId, Kind = kind, OccurredOn = Today };

    /// <summary>Saves one care entry and, for the notable kinds, records it on the plant's history.</summary>
    public async Task LogAsync(CareLog entry, Func<Enum, string> label)
    {
        await logs.SaveAsync(entry);
        await RecordAsync(entry, label);
    }

    /// <summary>
    /// The same thing done to several plants at once, e.g. "fertilised all the semi-hydro
    /// plants". Each plant gets its own entry so its own history stays right.
    /// </summary>
    public async Task<int> LogManyAsync(
        IEnumerable<Guid> plantIds,
        CareKind kind,
        DateOnly occurredOn,
        string? notes,
        Func<Enum, string> label,
        int? moisture = null,
        IReadOnlyList<ProductDose>? products = null)
    {
        var used = Tidy(products);
        var entries = plantIds
            .Distinct()
            .Select(id => new CareLog
            {
                PlantId = id,
                Kind = kind,
                OccurredOn = occurredOn,
                Moisture = moisture,
                // Each entry gets its own copy, so changing one later can't change the others
                Products = [.. used.Select(p => p.Copy())],
                Notes = Clean(notes)
            })
            .ToList();

        // Check everything before saving anything, the way potting up does
        if (entries.SelectMany(e => e.Validate(Today)).FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        foreach (var entry in entries)
        {
            await logs.SaveAsync(entry);
            await RecordAsync(entry, label);
        }

        return entries.Count;
    }

    public Task DeleteAsync(Guid id) => logs.DeleteAsync(id);

    /// <summary>
    /// "Repotted", "Watered: rainwater" or "Fertilised: Hydro fertiliser, 2 ml/L · half strength".
    /// </summary>
    public static string Describe(CareLog entry, Func<Enum, string> label)
    {
        var what = label(entry.Kind);
        if (entry.Kind == CareKind.MoistureReading && entry.Moisture is { } moisture)
            what = $"{what}: {moisture}/10";

        var notes = Clean(entry.Notes);
        if (entry.Products.Count == 0)
            return notes is null ? what : $"{what}: {notes}";

        var products = string.Join(" + ", entry.Products);
        return notes is null ? $"{what}: {products}" : $"{what}: {products} · {notes}";
    }

    private Task RecordAsync(CareLog entry, Func<Enum, string> label)
    {
        if (!CareKinds.IsNotable(entry.Kind))
            return Task.CompletedTask;

        return timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = entry.PlantId,
            Kind = TimelineKind.Change,
            OccurredAt = MomentOf(entry.OccurredOn),
            Text = Describe(entry, label)
        });
    }

    // A date without a time: "now" for today, otherwise noon so it lands on the right day
    private DateTimeOffset MomentOf(DateOnly date) =>
        date == Today ? time.GetUtcNow() : new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Rows without a name are dropped rather than refused, the way a blank soil mix row is
    private static List<ProductDose> Tidy(IReadOnlyList<ProductDose>? products) =>
        [.. (products ?? []).Where(p => !string.IsNullOrWhiteSpace(p.Name)).Select(p =>
        {
            var copy = p.Copy();
            copy.Name = p.Name.Trim();
            return copy;
        })];
}
