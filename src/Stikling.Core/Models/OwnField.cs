using Strings = Stikling.Core.Resources;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What a field of the person's own holds.</summary>
[JsonConverter(typeof(TolerantEnumOrDefaultConverter<OwnFieldKind>))]
public enum OwnFieldKind
{
    Text,
    Number,
    Date,
    Choice
}

/// <summary>What a field of the person's own is on. Only plants for now, so propagations can be added later without changing what is stored.</summary>
[JsonConverter(typeof(TolerantEnumOrDefaultConverter<OwnFieldSubject>))]
public enum OwnFieldSubject
{
    Plants
}

/// <summary>Writes an enum as text and reads a name this version doesn't know as the first value.</summary>
public sealed class TolerantEnumOrDefaultConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<T>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        reader.TrySkip();
        return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>
/// A field the app doesn't have, like "Clone name" or "Awards", that someone added themselves and
/// fills in on their plants. A plant keeps its value in <see cref="Plant.OwnFields"/> under the id
/// of the field, as text, so removing a field leaves the values where they are.
/// </summary>
public sealed class OwnField : Entity
{
    // Must never change. The id of a new field is made from its name with this, so that two devices
    // that both add "Clone name" make the same record. Changing it would make them two again.
    public static readonly Guid IdNamespace = new("daac4675-de30-4de0-94a3-9ffa6a0db7fc");

    public const int MaxNameLength = 40;
    public const int MaxChoices = 50;

    public string Name { get; set; } = "";

    public OwnFieldKind Kind { get; set; } = OwnFieldKind.Text;

    /// <summary>The choices of a Choice field, in the order they are shown. Empty for the other kinds.</summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>What the field is on. Only plants for now.</summary>
    public OwnFieldSubject For { get; set; } = OwnFieldSubject.Plants;

    /// <summary>The id a new field with this name gets. The same for "Clone name" and " clone name ".</summary>
    public static Guid IdFor(string name) => NameBasedId.For(IdNamespace, name.Trim().ToLowerInvariant());

    public OwnField Copy()
    {
        var copy = (OwnField)MemberwiseClone();
        copy.Choices = [.. Choices];
        return copy;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(Strings.Shared.FieldNameRequired);
        else if (Name.Trim().Length > MaxNameLength)
            errors.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.FieldNameLong, MaxNameLength));

        var choices = Choices ?? [];
        if (choices.Any(c => string.IsNullOrWhiteSpace(c)))
            errors.Add(Strings.Shared.FieldChoiceEmpty);
        if (choices.Any(c => c.Trim().Length > MaxNameLength))
            errors.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.FieldChoiceLong, MaxNameLength));
        if (choices.Count > MaxChoices)
            errors.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.FieldTooManyChoices, MaxChoices));
        if (choices.Select(c => c.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase).Count() != choices.Count)
            errors.Add(Strings.Shared.FieldChoiceTwice);
        return errors;
    }
}
