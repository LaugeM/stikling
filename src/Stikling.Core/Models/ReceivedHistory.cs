using Stikling.Core.Sharing;

namespace Stikling.Core.Models;

/// <summary>The history a plant or propagation had before someone got it, copied once from the giver's share link and never updated.</summary>
public sealed class ReceivedHistory : Entity
{
    /// <summary>The kind of the friend's own plant or propagation.</summary>
    public SubjectType SubjectType { get; set; }

    /// <summary>The friend's own plant or propagation.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>The giver's name for it.</summary>
    public string Name { get; set; } = "";

    /// <summary>The day it was added here.</summary>
    public DateOnly ArrivedOn { get; set; }

    /// <summary>Oldest first.</summary>
    public List<ReceivedItem> Items { get; set; } = [];
}

/// <summary>One item of a <see cref="ReceivedHistory"/>, as the giver's page showed it.</summary>
public sealed class ReceivedItem
{
    public DateOnly Day { get; set; }

    /// <summary>What the giver's page said after the date, frozen, like "day 24".</summary>
    public string? DayCount { get; set; }

    public ShareItemKind Kind { get; set; }
    public string? Text { get; set; }

    /// <summary>The friend's photo ids, see <see cref="Photo.ReceivedHistoryId"/>.</summary>
    public List<Guid> PhotoIds { get; set; } = [];
}
