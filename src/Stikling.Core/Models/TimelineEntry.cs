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
    Change
}

/// <summary>
/// One event in the history of a plant or propagation. Entries are added, not edited:
/// the history stays a faithful record of what happened when.
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
}
