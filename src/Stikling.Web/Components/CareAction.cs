namespace Stikling.Web.Components;

/// <summary>
/// One tap on Today that logged care from an interval: the entries it made, so Undo can take
/// them back, and the plants it was for.
/// </summary>
/// <param name="All">Whether it was Done for all, which turns the whole card into a Done state.</param>
public sealed record CareAction(bool All, IReadOnlyList<Guid> EntryIds, IReadOnlyList<Stikling.Core.Care.IntervalItem> Items);

/// <summary>What the card asks Today to log.</summary>
public sealed record CareLogRequest(Stikling.Core.Care.IntervalGroup Group, IReadOnlyList<Stikling.Core.Care.IntervalItem> Items, bool All);

/// <summary>What the card asks Today to put off.</summary>
public sealed record CarePutOffRequest(Stikling.Core.Care.IntervalItem Item, int Days);
