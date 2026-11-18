using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Names;

public interface INameNoteRepository : IRepository<NameNote>
{
    /// <summary>Every note stored, deleted ones too, so writing on a name again can bring its note back.</summary>
    Task<IReadOnlyList<NameNote>> GetStoredAsync();
}

/// <summary>
/// The person's own notes on a genus, species or cultivar. A note is found by the name it is on, with
/// an old name followed to the one it goes by now, so a plant that still has the old name finds the
/// note written on the current one.
/// </summary>
public sealed class NameNoteService(INameNoteRepository notes, PlantNameService names)
{
    /// <summary>
    /// The key a note on this name has, from names that are already the current ones. Null when the
    /// level needs a part the name doesn't have. The species is left out of a cultivar's key, since
    /// a cultivar belongs to its genus and plants of it aren't always written down with the species.
    /// </summary>
    public static string? KeyOf(NameLevel level, string? genus, string? species, string? cultivar)
    {
        var g = NameKey.Of(genus);
        if (g.Length == 0)
            return null;
        switch (level)
        {
            case NameLevel.Genus:
                return g;
            case NameLevel.Species:
                var s = NameKey.Of(species);
                return s.Length == 0 ? null : $"{g} {s}";
            default:
                var c = NameKey.Of(PlantNames.Cultivar(cultivar));
                return c.Length == 0 ? null : $"{g}|{c}";
        }
    }

    /// <summary>The note on this name, or null when there is none.</summary>
    public async Task<NameNote?> GetAsync(NameLevel level, string? genus, string? species = null, string? cultivar = null)
    {
        var (g, s) = (await names.GetDictionaryAsync()).Current(genus, species);
        var key = KeyOf(level, g, s, cultivar);
        return key is null ? null : await notes.GetAsync(NameNote.IdFor(level, key));
    }

    /// <summary>The notes a plant's names have, genus first and cultivar last. Only the ones that are written.</summary>
    public async Task<IReadOnlyList<NameNote>> ForNameAsync(string? genus, string? species, string? cultivar)
    {
        var result = new List<NameNote>();
        foreach (var level in Enum.GetValues<NameLevel>())
        {
            if (await GetAsync(level, genus, species, cultivar) is { } note)
                result.Add(note);
        }
        return result;
    }

    /// <summary>
    /// Writes the note on a name. Text with nothing in it removes the note. A note that was removed
    /// is brought back by writing on the name again, since both have the same id. Returns the note,
    /// or null when it was removed.
    /// </summary>
    public async Task<NameNote?> SaveAsync(NameLevel level, string? genus, string? species, string? cultivar, string? text)
    {
        var (g, s) = (await names.GetDictionaryAsync()).Current(genus, species);
        var key = KeyOf(level, g, s, cultivar) ?? throw new InvalidOperationException(Strings.NameNotes.NoteNeedsName);
        var id = NameNote.IdFor(level, key);
        var trimmed = (text ?? "").Trim();

        var holder = (await notes.GetStoredAsync()).FirstOrDefault(n => n.Id == id);
        if (trimmed.Length == 0)
        {
            if (holder is { IsDeleted: false })
                await notes.DeleteAsync(id);
            return null;
        }

        if (holder is { IsDeleted: false } && holder.Text == trimmed)
            return holder;

        var note = holder ?? new NameNote { Id = id };
        note.Level = level;
        note.Key = key;
        note.Text = trimmed;
        note.DeletedAt = null;
        if (note.Validate().Count > 0)
            throw new InvalidOperationException(note.Validate()[0]);
        await notes.SaveAsync(note);
        return note;
    }
}
