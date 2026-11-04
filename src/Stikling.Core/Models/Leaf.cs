using System.Text.Json;
using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// How variegated a new leaf is, from a leaf that has gone green again to one with no green at all.
/// Kept as text, so the order here is only the order the choices are offered in.
/// </summary>
public enum Variegation
{
    /// <summary>The leaf has lost its variegation and is plain green.</summary>
    Reverted,

    Low,

    Medium,

    High,

    AllWhite
}

/// <summary>Writes the rating as text and reads a name this version doesn't know as null.</summary>
public sealed class TolerantVariegationConverter : JsonConverter<Variegation?>
{
    public override bool HandleNull => true;

    public override Variegation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<Variegation>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        // Skip throws when a backup is read from a stream in parts. A converter always gets the whole value, so TrySkip is enough.
        reader.TrySkip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Variegation? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteStringValue(v.ToString());
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// A new leaf on a plant or a cutting, with how variegated it is. Each leaf is a record of its own,
/// so two devices logging leaves only add records and never overwrite each other.
/// </summary>
public sealed class Leaf : Entity
{
    /// <summary>Plant or Propagation.</summary>
    public SubjectType SubjectType { get; set; } = SubjectType.Plant;

    public Guid SubjectId { get; set; }

    public DateOnly On { get; set; }

    /// <summary>
    /// Null when a later version wrote a rating this one doesn't know. Such a leaf is kept but
    /// ignored by the log and the trend, so it isn't lost and doesn't stop anything loading.
    /// </summary>
    [JsonConverter(typeof(TolerantVariegationConverter))]
    public Variegation? Rating { get; set; }

    public string? Note { get; set; }

    /// <summary>The history line written with the leaf, so removing the leaf takes it off again.</summary>
    public Guid? TimelineEntryId { get; set; }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (SubjectId == Guid.Empty)
            errors.Add(Strings.Pests.LeafNeedsSubject);
        if (Rating is null)
            errors.Add(Strings.Pests.LeafPickRating);
        if (On > today)
            errors.Add(Strings.Pests.LeafInFuture);
        return errors;
    }
}
