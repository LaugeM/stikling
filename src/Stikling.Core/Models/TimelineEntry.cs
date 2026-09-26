using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What a timeline entry or photo belongs to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubjectType>))]
public enum SubjectType
{
    Plant,
    Propagation
}

[JsonConverter(typeof(JsonStringEnumConverter<TimelineKind>))]
public enum TimelineKind
{
    /// <summary>The plant or propagation was added.</summary>
    Created,

    /// <summary>A note written by the user, optionally with photos.</summary>
    Note,

    /// <summary>Photos added without a note.</summary>
    Photo,

    /// <summary>Recorded automatically when details such as status, room or medium change.</summary>
    Change,

    /// <summary>On a parent plant: a propagation was taken from it.</summary>
    Propagated
}

/// <summary>
/// One event in the history of a plant or propagation. Notes and photos can be corrected
/// afterwards, but the version they replace is kept in <see cref="Edits"/>, so the history
/// still shows what was written at the time.
/// </summary>
public sealed class TimelineEntry : Entity
{
    public SubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>When it happened (can be earlier than when it was recorded).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public TimelineKind Kind { get; set; }

    public string? Text { get; set; }

    public List<Guid> PhotoIds { get; set; } = [];

    /// <summary>
    /// Another plant or propagation the entry is about, shown as a link. E.g. the propagation
    /// taken from a plant, or the propagation a plant was potted up from.
    /// </summary>
    public SubjectType? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }

    /// <summary>Earlier versions of the entry, oldest first. Empty when it was never corrected.</summary>
    public List<TimelineEdit> Edits { get; set; } = [];
}

/// <summary>How a timeline entry read before it was corrected.</summary>
public sealed class TimelineEdit
{
    public string? Text { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>When the correction was made.</summary>
    public DateTimeOffset ReplacedAt { get; set; }
}
