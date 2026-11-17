using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>A stretch of a note: plain text, or the "@Name" of a mention.</summary>
public sealed record MentionPart(string Text, Mention? Mention);

/// <summary>A record the @ picker offers.</summary>
public sealed record MentionChoice(SubjectType Kind, Guid Id, string Name, string? Detail);

/// <summary>
/// The rules for @ links in notes. The text keeps a plain "@Name" and the entry keeps the
/// <see cref="Mention"/>s, so a version of the app that doesn't know about them still reads the note.
/// A name is only a link while the text still has "@Name" in it.
/// </summary>
public static class Mentions
{
    /// <summary>How many choices the picker shows at most.</summary>
    public const int MostChoices = 6;

    /// <summary>The text that goes in a note for a mention.</summary>
    public static string TextOf(Mention mention) => "@" + mention.Name;

    /// <summary>
    /// The @ the person is typing: where it starts and what follows it up to the caret, or null when the caret
    /// isn't in one. An @ in the middle of a word, like in an address, doesn't count, and neither does one whose
    /// word has gone on past a line break.
    /// </summary>
    public static (int Start, string Query)? Typing(string? text, int caret)
    {
        if (string.IsNullOrEmpty(text) || caret < 1 || caret > text.Length)
            return null;

        var at = text.LastIndexOf('@', caret - 1);
        if (at < 0)
            return null;
        if (at > 0 && !char.IsWhiteSpace(text[at - 1]) && !char.IsPunctuation(text[at - 1]))
            return null;

        var query = text[(at + 1)..caret];
        // A name can have spaces, but a line break or a long run means it is just text
        if (query.Contains('\n') || query.Length > 40)
            return null;
        return (at, query);
    }

    /// <summary>The records whose name starts a word with what was typed after the @, best matches first.</summary>
    public static IReadOnlyList<MentionChoice> Choose(string query, IEnumerable<MentionChoice> all)
    {
        var typed = query.Trim();
        return [.. all
            .Select(c => (Choice: c, Rank: Rank(c.Name, typed)))
            .Where(x => x.Rank >= 0)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Choice.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Choice)
            .Take(MostChoices)];
    }

    // 0 for a name that starts with it, 1 for one with a word that does, 2 for one that has it somewhere, -1 for no match
    private static int Rank(string name, string typed)
    {
        if (typed.Length == 0)
            return 0;
        if (name.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase))
            return 0;
        if (name.Split([' ', '-', '\'', '(', ')']).Any(w => w.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase)))
            return 1;
        return name.Contains(typed, StringComparison.CurrentCultureIgnoreCase) ? 2 : -1;
    }

    /// <summary>The mentions with another added, replacing one of the same name, so a name never means two records.</summary>
    public static List<Mention> With(IEnumerable<Mention>? mentions, Mention added) =>
        [.. (mentions ?? []).Where(m => !string.Equals(m.Name, added.Name, StringComparison.Ordinal)), added];

    /// <summary>
    /// The mentions whose "@Name" is still in the text, or null when none are. Used when a note is saved or
    /// corrected, so a name that was deleted from the text stops being a link and counting as a mention.
    /// </summary>
    public static List<Mention>? Keep(string? text, IEnumerable<Mention>? mentions)
    {
        if (string.IsNullOrEmpty(text) || mentions is null)
            return null;

        var kept = new List<Mention>();
        foreach (var part in Split(text, [.. mentions]))
        {
            if (part.Mention is { } found && !kept.Contains(found))
                kept.Add(found);
        }
        return kept.Count == 0 ? null : kept;
    }

    /// <summary>
    /// The text in stretches, each "@Name" that matches a mention on its own. The text is never changed, so
    /// joining the parts gives it back. The longest name wins where two start at the same place.
    /// </summary>
    public static IReadOnlyList<MentionPart> Split(string? text, IReadOnlyList<Mention>? mentions)
    {
        if (string.IsNullOrEmpty(text))
            return [];
        if (mentions is not { Count: > 0 })
            return [new MentionPart(text, null)];

        var ordered = mentions.Where(m => m.Name.Length > 0).OrderByDescending(m => m.Name.Length).ToList();
        var parts = new List<MentionPart>();
        var plain = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '@' && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
            {
                var at = i;
                var hit = ordered.FirstOrDefault(m => Matches(text, at, m));
                if (hit is not null)
                {
                    if (i > plain)
                        parts.Add(new MentionPart(text[plain..i], null));
                    var length = hit.Name.Length + 1;
                    parts.Add(new MentionPart(text.Substring(i, length), hit));
                    i += length;
                    plain = i;
                    continue;
                }
            }
            i++;
        }

        if (plain < text.Length)
            parts.Add(new MentionPart(text[plain..], null));
        return parts;
    }

    // "@Name" at the position, not followed by more of a word, so "@Fern" isn't found inside "@Fernando"
    private static bool Matches(string text, int at, Mention mention)
    {
        var end = at + 1 + mention.Name.Length;
        if (end > text.Length || string.CompareOrdinal(text, at + 1, mention.Name, 0, mention.Name.Length) != 0)
            return false;
        return end == text.Length || !char.IsLetterOrDigit(text[end]);
    }

    /// <summary>Whether two lists name the same records in the same order.</summary>
    public static bool Same(IReadOnlyList<Mention>? a, IReadOnlyList<Mention>? b) =>
        (a ?? []).SequenceEqual(b ?? []);

    /// <summary>The start of a note for the Mentioned in card: the first line, cut at a word when it is long.</summary>
    public static string Snippet(string? text, int most = 90)
    {
        var line = (text ?? "").Trim();
        var newline = line.IndexOfAny(['\r', '\n']);
        if (newline >= 0)
            line = line[..newline].TrimEnd();
        if (line.Length <= most)
            return line;
        var cut = line.LastIndexOf(' ', most);
        return line[..(cut > most / 2 ? cut : most)].TrimEnd() + "…";
    }
}
