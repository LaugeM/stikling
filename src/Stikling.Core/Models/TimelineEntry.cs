using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What a timeline entry or photo belongs to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubjectType>))]
public enum SubjectType
{
    Plant,
    Propagation,

    /// <summary>A pot from the library. Only photos belong to one, never a timeline entry.</summary>
    Pot,

    /// <summary>A wish on the wishlist. Only photos belong to one, never a timeline entry.</summary>
    Wish
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
/// What a <see cref="TimelineKind.Change"/> entry was about, when it's more than a plain update.
/// Kept apart from <see cref="TimelineKind"/> so older versions, which fail on a kind they don't
/// know, still load the entry and show it as "Updated".
/// </summary>
public enum TimelineEvent
{
    /// <summary>Units of a propagation were potted up as plants.</summary>
    PottedUp,

    /// <summary>Units of a propagation didn't make it.</summary>
    Failed,

    /// <summary>Looked at during a round, or on its own, with nothing else to log.</summary>
    LookedOver,

    /// <summary>Units of a propagation were given to someone.</summary>
    GaveAway,

    /// <summary>The plant or propagation was given to you. On the Created entry that starts its history.</summary>
    Arrived,

    /// <summary>A plant got buds, came into flower or finished flowering.</summary>
    Flowering,

    /// <summary>A problem on a plant was opened, updated, closed or came back.</summary>
    Problem,

    /// <summary>A name that was unsure (cf., aff., sp. or lost) was found out.</summary>
    Identified
}

/// <summary>Writes the event as text and reads a name this version doesn't know as null.</summary>
public sealed class TolerantTimelineEventConverter : JsonConverter<TimelineEvent?>
{
    public override bool HandleNull => true;

    public override TimelineEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<TimelineEvent>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        // Skip throws when a backup is read from a stream in parts. A converter always gets the whole value, so TrySkip is enough.
        reader.TrySkip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, TimelineEvent? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>Why units of a propagation didn't make it. Picked when marking them failed, and never required.</summary>
public enum FailReason
{
    Rotted,
    DriedOut,
    Pests,
    Mould,
    NeverRooted,
    DontKnow
}

/// <summary>Writes the reason as text and reads one this version doesn't know as null.</summary>
public sealed class TolerantFailReasonConverter : JsonConverter<FailReason?>
{
    public override bool HandleNull => true;

    public override FailReason? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<FailReason>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        reader.TrySkip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, FailReason? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
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

    /// <summary>
    /// What a change entry was about (a pot-up, a failure or a look over). Null on entries from before this was
    /// kept; <see cref="Timeline.TimelineEvents.Of"/> recognises those from their text.
    /// </summary>
    [JsonConverter(typeof(TolerantTimelineEventConverter))]
    public TimelineEvent? Event { get; set; }

    /// <summary>Why the units failed. Only on a <see cref="TimelineEvent.Failed"/> entry, and null when none was picked or on older entries.</summary>
    [JsonConverter(typeof(TolerantFailReasonConverter))]
    public FailReason? FailReason { get; set; }

    /// <summary>How many units the entry is about. Only on a failed entry, and null on older ones.</summary>
    public int? Count { get; set; }

    public List<Guid> PhotoIds { get; set; } = [];

    /// <summary>
    /// Another plant or propagation the entry is about, shown as a link. E.g. the propagation
    /// taken from a plant, or the propagation a plant was potted up from.
    /// </summary>
    public SubjectType? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }

    /// <summary>
    /// The day a note comes back on Today ("Bring it back"). Only on notes, and null on every entry
    /// from before this was kept. Changing it isn't a correction, so it adds nothing to <see cref="Edits"/>.
    /// </summary>
    public DateOnly? ComesBackOn { get; set; }

    /// <summary>The day the note that came back was ticked Done on Today. Null until then.</summary>
    public DateOnly? CameBackDoneOn { get; set; }

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
