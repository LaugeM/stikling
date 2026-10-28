using System.Text;
using Stikling.Core.Models;

namespace Stikling.Core.Search;

/// <summary>
/// Everything that can be searched, read once. <see cref="Find"/> then runs in memory, so it can be
/// called on every keystroke.
/// </summary>
public sealed class SearchIndex
{
    private const int BeforeMatch = 60;
    private const int AfterMatch = 80;

    /// <summary>Where else than in its name a record can match, like a tag, its number or its notes.</summary>
    internal sealed record Extra(Func<string, bool> Matches, string Reason, bool IsNotes = false);

    /// <summary>A record that is found by its names, with the places beyond them that count.</summary>
    internal sealed record Doc(SearchItem Item, string Names, IReadOnlyList<Extra> Extras)
    {
        public string FoldedTitle { get; } = SearchText.Fold(Item.Title);
    }

    /// <summary>A note, with its text folded once.</summary>
    internal sealed record NoteDoc(
        Guid RecordId,
        string Href,
        NoteSource Source,
        DateTimeOffset Date,
        SubjectType? OwnerType,
        Guid OwnerId,
        string OwnerName,
        string Label,
        string Text)
    {
        public FoldedText Folded { get; } = FoldedText.From(Text);
    }

    private readonly IReadOnlyDictionary<SearchKind, IReadOnlyList<Doc>> docs;
    private readonly IReadOnlyList<NoteDoc> notes;

    internal SearchIndex(IReadOnlyDictionary<SearchKind, IReadOnlyList<Doc>> docs, IReadOnlyList<NoteDoc> notes)
    {
        this.docs = docs;
        this.notes = notes;
    }

    /// <summary>
    /// Every word has to be found, ignoring case and accents. Notes are only searched when every word
    /// has at least two letters, since one letter would find nearly every note.
    /// </summary>
    public SearchResults Find(string? query)
    {
        var words = SearchText.Words(query);
        if (words.Count == 0)
            return SearchResults.Empty;

        var notesOk = words.All(w => w.Length >= 2);
        var groups = new Dictionary<SearchKind, IReadOnlyList<SearchItem>>();
        foreach (var (kind, list) in docs)
            groups[kind] = Order(list.Select(d => Match(d, words, notesOk)).OfType<Found>(), words[0]);

        groups[SearchKind.Note] = notesOk ? FindNotes(words) : [];
        return new SearchResults(groups);
    }

    private sealed record Found(Doc Doc, SearchItem Item);

    private static Found? Match(Doc doc, IReadOnlyList<string> words, bool notesOk)
    {
        string? reason = null;
        foreach (var word in words)
        {
            if (doc.Names.Contains(word, StringComparison.Ordinal))
                continue;
            var extra = doc.Extras.FirstOrDefault(e => (notesOk || !e.IsNotes) && e.Matches(word));
            if (extra is null)
                return null;
            reason ??= extra.Reason;
        }

        return new Found(doc, reason is null ? doc.Item : doc.Item with { Meta = reason });
    }

    // Not gone first, then the names that start with what was typed, then A to Z
    private static IReadOnlyList<SearchItem> Order(IEnumerable<Found> found, string first) =>
        [.. found
            .OrderBy(f => f.Item.Gone)
            .ThenBy(f => !f.Doc.FoldedTitle.StartsWith(first, StringComparison.Ordinal))
            .ThenBy(f => f.Item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => f.Item)];

    private IReadOnlyList<SearchItem> FindNotes(IReadOnlyList<string> words)
    {
        var found = new List<SearchItem>();
        foreach (var note in notes)
        {
            if (!words.All(w => note.Folded.Text.Contains(w, StringComparison.Ordinal)))
                continue;

            var (snippet, ranges) = Snippet(note, words);
            found.Add(new SearchItem(
                SearchKind.Note, note.RecordId, note.OwnerName, null, null, false, note.Href,
                new SearchNote(note.Source, note.Date, note.OwnerType, note.OwnerId, note.OwnerName, note.Label, snippet, ranges)));
        }

        return [.. found
            .OrderByDescending(i => i.Note!.Date)
            .ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// About 60 characters before the first match and 80 after, cut between words, with the matches
    /// found in what is left. The ranges count from the start of the snippet, ellipsis included.
    /// </summary>
    internal static (string Snippet, IReadOnlyList<TextRange> Ranges) Snippet(NoteDoc note, IReadOnlyList<string> words)
    {
        var text = note.Text;
        var folded = note.Folded;

        var firstAt = int.MaxValue;
        var firstLength = 0;
        foreach (var word in words)
        {
            var at = folded.Text.IndexOf(word, StringComparison.Ordinal);
            if (at >= 0 && (at < firstAt || at == firstAt && word.Length > firstLength))
            {
                firstAt = at;
                firstLength = word.Length;
            }
        }

        var matchStart = folded.Starts[firstAt];
        var matchEnd = folded.Ends[firstAt + firstLength - 1];

        var start = Math.Max(0, matchStart - BeforeMatch);
        if (start > 0 && text[start - 1] != ' ')
        {
            var space = text.IndexOf(' ', start);
            start = space >= 0 && space < matchStart ? space + 1 : text.LastIndexOf(' ', matchStart) + 1;
        }

        var end = Math.Min(text.Length, matchEnd + AfterMatch);
        if (end < text.Length && text[end] != ' ')
        {
            var space = text.LastIndexOf(' ', end - 1);
            if (space >= matchEnd)
                end = space;
            else
            {
                var next = text.IndexOf(' ', end);
                end = next >= 0 ? next : text.Length;
            }
        }

        var prefix = start > 0 ? "…" : "";
        var suffix = end < text.Length ? "…" : "";
        var snippet = prefix + text[start..end] + suffix;

        var ranges = new List<(int Start, int End)>();
        foreach (var word in words)
        {
            for (var at = folded.Text.IndexOf(word, StringComparison.Ordinal); at >= 0;
                at = folded.Text.IndexOf(word, at + 1, StringComparison.Ordinal))
            {
                var from = Math.Max(start, folded.Starts[at]);
                var to = Math.Min(end, folded.Ends[at + word.Length - 1]);
                if (to > from)
                    ranges.Add((from, to));
            }
        }

        var merged = new List<TextRange>();
        var openStart = -1;
        var openEnd = -1;
        foreach (var (from, to) in ranges.OrderBy(r => r.Start))
        {
            if (openStart >= 0 && from <= openEnd)
                openEnd = Math.Max(openEnd, to);
            else
            {
                if (openStart >= 0)
                    merged.Add(new TextRange(openStart - start + prefix.Length, openEnd - openStart));
                openStart = from;
                openEnd = to;
            }
        }

        if (openStart >= 0)
            merged.Add(new TextRange(openStart - start + prefix.Length, openEnd - openStart));
        return (snippet, merged);
    }

    /// <summary>Whitespace of any kind, runs of it included, becomes a single space.</summary>
    internal static string Collapse(string text)
    {
        var result = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
                space = result.Length > 0;
            else
            {
                if (space)
                    result.Append(' ');
                space = false;
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
