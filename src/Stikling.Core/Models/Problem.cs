using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// What someone sees wrong on a plant. A problem keeps these as text (see <see cref="Problem.Signs"/>),
/// so the order here is only the order they are offered in.
/// </summary>
public enum ProblemSign
{
    YellowLeaves,
    BrownTips,
    Drooping,
    Spots,
    LeafDrop,
    RootRot,
    MushyStem,
    Sunburn,
    Leggy,
    Edema,
    Other
}

/// <summary>What was done about a problem, the quick picks. Kept as text on the update, like the signs.</summary>
public enum ProblemAction
{
    CutOffLeaves,
    MovedIt,
    WateredLess,
    WateredMore,
    Repotted,
    CutAwayRoots,
    MoreLight,
    LessLight
}

/// <summary>How a problem looks now compared to before.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProblemTrend>))]
public enum ProblemTrend
{
    Better,
    Same,
    Worse
}

/// <summary>How a problem ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProblemOutcome>))]
public enum ProblemOutcome
{
    GotBetter,
    WentAway,
    DidntHelp,
    Died
}

/// <summary>Writes the trend as text and reads a name this version doesn't know as null.</summary>
public sealed class TolerantProblemTrendConverter : JsonConverter<ProblemTrend?>
{
    public override bool HandleNull => true;

    public override ProblemTrend? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<ProblemTrend>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ProblemTrend? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>Writes the outcome as text and reads a name this version doesn't know as null.</summary>
public sealed class TolerantProblemOutcomeConverter : JsonConverter<ProblemOutcome?>
{
    public override bool HandleNull => true;

    public override ProblemOutcome? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<ProblemOutcome>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ProblemOutcome? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// Something wrong on a plant, from when it was seen until it was closed. What happened since is
/// kept as <see cref="ProblemUpdate"/> records, so two devices adding to the same problem only
/// add records. A problem is open while <see cref="ClosedOn"/> is null.
/// </summary>
public sealed class Problem : Entity
{
    /// <summary>Only plants for now. The field is there so propagations can come later.</summary>
    public SubjectType SubjectType { get; set; } = SubjectType.Plant;

    public Guid SubjectId { get; set; }

    public DateOnly StartedOn { get; set; }

    /// <summary>
    /// The names of <see cref="ProblemSign"/> values. Plain strings on purpose: a problem is saved
    /// again when it's edited or closed, and an older version would drop a sign it doesn't know if
    /// it went through an enum converter. Read them with <see cref="KnownSigns"/>.
    /// </summary>
    public List<string> Signs { get; set; } = [];

    public string? Note { get; set; }

    /// <summary>When to look at it again, set when it was opened.</summary>
    public DateOnly? LookAgainOn { get; set; }

    /// <summary>The day it ended. Open while null.</summary>
    public DateOnly? ClosedOn { get; set; }

    [JsonConverter(typeof(TolerantProblemOutcomeConverter))]
    public ProblemOutcome? Outcome { get; set; }

    /// <summary>The history line written when it was opened.</summary>
    public Guid? TimelineEntryId { get; set; }

    /// <summary>The history line written when it was closed.</summary>
    public Guid? ClosedTimelineEntryId { get; set; }

    /// <summary>
    /// The day it was last opened again after being closed, and the moment, so a look again day
    /// set on an update before it closed doesn't come back with it. Null when it never was.
    /// </summary>
    public DateOnly? ReopenedOn { get; set; }

    public DateTimeOffset? ReopenedAt { get; set; }

    [JsonIgnore]
    public bool IsOpen => ClosedOn is null;

    /// <summary>The signs this version knows, in the order they were stored. Others are skipped, not removed.</summary>
    [JsonIgnore]
    public IReadOnlyList<ProblemSign> KnownSigns =>
        Signs.Select(s => Enum.TryParse<ProblemSign>(s, out var sign) && Enum.IsDefined(sign) ? (ProblemSign?)sign : null)
            .OfType<ProblemSign>().ToList();

    public Problem Copy()
    {
        var copy = (Problem)MemberwiseClone();
        copy.Signs = [.. Signs];
        return copy;
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (SubjectId == Guid.Empty)
            errors.Add("A problem belongs to a plant.");
        if (Signs.Count == 0)
            errors.Add("Pick what you see.");
        if (StartedOn > today)
            errors.Add("That start date is in the future.");
        if (ClosedOn is { } closed)
        {
            if (closed > today)
                errors.Add("That date is in the future.");
            if (closed < StartedOn)
                errors.Add("A problem can't end before it started.");
        }
        // A closed problem can have no outcome here when a newer version wrote one this version
        // doesn't know, so only an outcome on an open problem is refused
        if (ClosedOn is null && Outcome is not null)
            errors.Add("Say how it ended.");
        if (LookAgainOn is { } again && again < StartedOn)
            errors.Add("Look again after it started.");
        return errors;
    }
}
