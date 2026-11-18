using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.People;

/// <summary>What a person may do in a collection, as the API names it.</summary>
[JsonConverter(typeof(CollectionRoleConverter))]
public enum CollectionRole
{
    /// <summary>Can only read, for example a plant sitter.</summary>
    Viewer,

    /// <summary>Can log care, add notes and photos, and undo their own entries.</summary>
    Helper,

    /// <summary>Can change everything.</summary>
    Editor,
}

public static class CollectionRoles
{
    /// <summary>Reads the API's text for a role. Text this version doesn't know reads as a viewer, the safest.</summary>
    public static CollectionRole Parse(string? text) =>
        Enum.TryParse<CollectionRole>(text, ignoreCase: true, out var role) && Enum.IsDefined(role) ? role : CollectionRole.Viewer;
}

/// <summary>Saved as text, and read leniently, so a role added later doesn't stop the registry from loading.</summary>
public sealed class CollectionRoleConverter : JsonConverter<CollectionRole>
{
    public override CollectionRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        CollectionRoles.Parse(reader.TokenType == JsonTokenType.String ? reader.GetString() : null);

    public override void Write(Utf8JsonWriter writer, CollectionRole value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
