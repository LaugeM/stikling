using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>A note that has come back on Today, with the name of what it is about.</summary>
public sealed record NoteComingBack(TimelineEntry Entry, string Name);

/// <summary>Which notes show on Today under "Notes coming back".</summary>
public static class NotesComingBack
{
    /// <summary>A note with a day to come back on that hasn't been ticked Done.</summary>
    public static bool IsWaiting(TimelineEntry entry) =>
        !entry.IsDeleted && entry.Kind == TimelineKind.Note && entry.ComesBackOn is not null && entry.CameBackDoneOn is null;

    /// <summary>
    /// The notes due on <paramref name="today"/> or before, oldest day first. Notes about a plant that isn't in the
    /// collection any more, a propagation that is finished, or something deleted are left out.
    /// </summary>
    public static IReadOnlyList<NoteComingBack> For(
        IEnumerable<TimelineEntry> entries, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, DateOnly today)
    {
        var names = new Dictionary<(SubjectType, Guid), string>();
        foreach (var plant in plants.Where(p => !p.IsDeleted && p.InCollection))
            names[(SubjectType.Plant, plant.Id)] = plant.DisplayName;
        foreach (var propagation in propagations.Where(p => !p.IsDeleted && p.IsActive))
            names[(SubjectType.Propagation, propagation.Id)] = propagation.ListName;

        return entries
            .Where(e => IsWaiting(e) && e.ComesBackOn <= today && names.ContainsKey((e.SubjectType, e.SubjectId)))
            .Select(e => new NoteComingBack(e, names[(e.SubjectType, e.SubjectId)]))
            .OrderBy(n => n.Entry.ComesBackOn)
            .ThenBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(n => n.Entry.OccurredAt)
            .ThenBy(n => n.Entry.Id)
            .ToList();
    }

    /// <summary>Same day next year. 29 February becomes 28 February.</summary>
    public static DateOnly SameDayNextYear(DateOnly day) => day.AddYears(1);

    /// <summary>The note's day and month in the next year that is still ahead of <paramref name="today"/>, so an old note gets a day that can be picked.</summary>
    public static DateOnly SameDayNextYear(DateOnly day, DateOnly today) => NextYear(day, today);

    /// <summary>A year on from <paramref name="day"/>, and once more for every year it has already gone by, so it is after <paramref name="today"/>.</summary>
    public static DateOnly NextYear(DateOnly day, DateOnly today)
    {
        var next = day.AddYears(1);
        while (next <= today)
            next = next.AddYears(1);
        return next;
    }
}

/// <summary>What the "Bring it back" control on a note holds.</summary>
public enum BringBackMode
{
    No,
    SameDayNextYear,
    OnADay
}

/// <summary>The choice made in the "Bring it back" control, worked out to a day for the note it is on.</summary>
public sealed class BringBackChoice
{
    public BringBackMode Mode { get; set; }

    /// <summary>The day typed in for <see cref="BringBackMode.OnADay"/>.</summary>
    public DateOnly? Picked { get; set; }

    public static BringBackChoice For(DateOnly? comesBackOn) =>
        comesBackOn is { } day ? new BringBackChoice { Mode = BringBackMode.OnADay, Picked = day } : new BringBackChoice();

    /// <summary>The day the note comes back on, or null when it isn't brought back (or no day is picked).</summary>
    public DateOnly? Resolve(DateOnly noteDay, DateOnly today) => Mode switch
    {
        BringBackMode.SameDayNextYear => NotesComingBack.SameDayNextYear(noteDay, today),
        BringBackMode.OnADay => Picked,
        _ => null
    };

    /// <summary>
    /// False when a day is asked for and isn't after <paramref name="today"/>. A day that is the same as the one
    /// <paramref name="stored"/> on the note is always fine, so a note that has come back can still be edited.
    /// </summary>
    public bool IsValid(DateOnly noteDay, DateOnly today, DateOnly? stored = null) =>
        Mode == BringBackMode.No || Resolve(noteDay, today) is { } day && (day > today || day == stored);
}
