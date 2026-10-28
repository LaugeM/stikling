using Stikling.Core.Models;

namespace Stikling.Core.Away;

/// <summary>One way of watering, and the steps for it.</summary>
/// <param name="Name">What the method is called, or null when it is the only one on the sheet and needs no label.</param>
public sealed record SitterStep(string? Name, string Text);

/// <summary>
/// The "Before you water" box. It is always printed, since it is the safety rule: the check comes
/// first, and watering steps only read "If it needs water".
/// </summary>
/// <param name="Check">The paragraphs about checking, which depend on whether there is a moisture meter.</param>
/// <param name="Steps">The steps for each way of watering that appears on the sheet. Empty when they are hidden.</param>
/// <param name="TopUp">The line about plants in water or on a reservoir, when any card tops up.</param>
public sealed record SitterBox(IReadOnlyList<string> Check, IReadOnlyList<SitterStep> Steps, string? TopUp)
{
    public const string Heading = "Before you water";

    public const string StepsHeading = "If it needs water";
}

/// <summary>A plant or propagation on the sheet, with the lines for its card.</summary>
/// <param name="Room">The room it is in, or null for none.</param>
/// <param name="Spot">The spot inside the room, or null.</param>
/// <param name="Place">"Room · Spot", or null when it has no place or places are hidden.</param>
/// <param name="PhotoId">The cover photo, or null.</param>
/// <param name="How">The short name of how to water it, or what to top up. Null when it is watered the usual way.</param>
/// <param name="Visits">The visits it is checked on. Empty without dates, or when it should be fine until you're back.</param>
/// <param name="DueBeforeYouGo">The pace says it is due before the first day away. Shown on screen only.</param>
/// <param name="Number">Its collection number, or null.</param>
/// <param name="CheckNote">"Check it each time you visit" without dates, "Should be fine until you're back" with none of the visits, otherwise null.</param>
public sealed record SitterSubject(
    Guid Id,
    bool IsPlant,
    string Name,
    string? Species,
    string? Room,
    string? Spot,
    string? Place,
    Guid? PhotoId,
    string? How,
    string? Meter,
    string? Pace,
    string? Note,
    IReadOnlyList<DateOnly> Visits,
    bool DueBeforeYouGo,
    string? CheckNote,
    string? Number = null)
{
    /// <summary>The name, with its collection number before it when it has one: "#117 Big Monstera".</summary>
    public string Label => Number is null ? Name : $"#{Number} {Name}";
}

/// <summary>Subjects under one heading in the order of the walk round, a room or "Elsewhere".</summary>
public sealed record SitterGroup(string Heading, IReadOnlyList<SitterSubject> Subjects);

/// <summary>One visit and what to check on it, grouped as on the sheet.</summary>
public sealed record SitterVisit(DateOnly Date, IReadOnlyList<SitterGroup> Groups)
{
    public int Count => Groups.Sum(g => g.Subjects.Count);
}

/// <summary>An active plant or propagation, whether or not it is on the sheet, for choosing what goes on it.</summary>
/// <param name="TopWatered">A plant watered from the top, which is the only kind with a way of watering to choose.</param>
public sealed record SitterCandidate(
    Guid Id, bool IsPlant, string Name, string? Place, bool OnSheet, bool TopWatered, WaterMethod? WaterBy);

/// <summary>Everything the page and the text writer show.</summary>
/// <param name="From">The first day away, or null when there is no stretch to put dates on.</param>
/// <param name="Until">The day back.</param>
/// <param name="Header">"20 December to 4 January · 6 plants, 2 cuttings · a visit twice a week".</param>
/// <param name="Visits">The dates of the visits. Empty when the sheet has no dates.</param>
/// <param name="ByVisit">What to check on each visit.</param>
/// <param name="DueBeforeYouGo">The names of plants whose pace says they are due before the first day away.</param>
/// <param name="AnyPace">At least one plant has a learned pace.</param>
public sealed record SitterSheetView(
    SheetLayout Layout,
    string Title,
    string Header,
    DateOnly? From,
    DateOnly? Until,
    string? GeneralNote,
    SitterBox Box,
    IReadOnlyList<DateOnly> Visits,
    IReadOnlyList<SitterGroup> Groups,
    IReadOnlyList<SitterVisit> ByVisit,
    IReadOnlyList<SitterCandidate> Candidates,
    IReadOnlyList<string> DueBeforeYouGo,
    bool AnyPace,
    bool ShowPhotos)
{
    public bool HasDates => Visits.Count > 0;

    public IEnumerable<SitterSubject> Subjects => Groups.SelectMany(g => g.Subjects);
}
