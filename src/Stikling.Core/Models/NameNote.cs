using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>What a note on a name is about: a genus, a species or a cultivar.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NameLevel>))]
public enum NameLevel
{
    Genus,
    Species,
    Cultivar
}

/// <summary>
/// The person's own note on a genus, a species or a cultivar, like "mine like it tight". It shows on
/// the name's page in the plant dictionary and under the name on every plant of it. The id is made
/// from the level and the name, so two devices that write a note on the same name write the same
/// record, and the newest edit wins. A note that is emptied is soft deleted and comes back when
/// the name gets a note again.
/// </summary>
public sealed class NameNote : Entity
{
    // Must never change. Every note's id is made from its name with this, so a note written on
    // another device lands on the same record. Changing it would make them two notes again.
    public static readonly Guid IdNamespace = new("3d9b7e52-6c1a-4f08-a5d4-91e2b7c80f36");

    /// <summary>The longest a note can be.</summary>
    public const int MaxLength = 2000;

    public NameLevel Level { get; set; }

    /// <summary>
    /// The name without case or quotes, with an old name followed to the one it goes by now: "monstera"
    /// for a genus, "monstera deliciosa" for a species, "monstera|thai constellation" for a cultivar.
    /// </summary>
    public string Key { get; set; } = "";

    public string Text { get; set; } = "";

    /// <summary>The id of the note for this name. Always the same for the same level and key.</summary>
    public static Guid IdFor(NameLevel level, string key) => NameBasedId.For(IdNamespace, $"{level}|{key}");

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Key))
            errors.Add(Strings.NameNotes.NoteNeedsName);
        if (Text.Length > MaxLength)
            errors.Add(Strings.NameNotes.NoteTooLong);
        return errors;
    }
}
