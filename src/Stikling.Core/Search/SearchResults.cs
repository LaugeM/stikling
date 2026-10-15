using Stikling.Core.Models;

namespace Stikling.Core.Search;

/// <summary>The kinds of result, in the order the groups are shown.</summary>
public enum SearchKind
{
    Plant,
    Propagation,
    Note,
    Wish,
    Pot,
    Mix,
    Feed,
    Product,
    TreatmentRecipe,
    Place,
    Tag
}

/// <summary>Where a note comes from, which decides the page it opens.</summary>
public enum NoteSource
{
    Timeline,
    PlantCare,
    PropagationCare,
    Problem,
    ProblemUpdate,
    PestCase,
    PestTreatment
}

/// <summary>A stretch of a snippet that matched, so the page can make it bold.</summary>
public readonly record struct TextRange(int Start, int Length);

/// <summary>What a note result adds to the item.</summary>
/// <param name="Date">The day the note is about, for newest first.</param>
/// <param name="OwnerType">Plant or propagation, and null for a pest case that isn't about one plant.</param>
/// <param name="OwnerId">The owner's id, or the case's id when there is no single owner.</param>
/// <param name="OwnerName">The plant or propagation, else the pest.</param>
/// <param name="Label">"Note", "Update", "Watered", "Problem" or "Pests".</param>
/// <param name="Snippet">A piece of the note around the first match, with an ellipsis at a cut end.</param>
/// <param name="Ranges">The matches in <see cref="Snippet"/>, in order and not overlapping.</param>
public sealed record SearchNote(
    NoteSource Source,
    DateTimeOffset Date,
    SubjectType? OwnerType,
    Guid OwnerId,
    string OwnerName,
    string Label,
    string Snippet,
    IReadOnlyList<TextRange> Ranges);

/// <summary>One row in the results.</summary>
/// <param name="Id">The record's id. Empty for a tag.</param>
/// <param name="Subtitle">The botanical name under a nickname, for plants and propagations.</param>
/// <param name="Meta">Why it matched when that isn't clear from the name ("tag windowsill"), else a line about it.</param>
/// <param name="Gone">A plant that left, a propagation that is done, or a wish that was got.</param>
/// <param name="Href">The page to open, relative to the app.</param>
public sealed record SearchItem(
    SearchKind Kind,
    Guid Id,
    string Title,
    string? Subtitle,
    string? Meta,
    bool Gone,
    string Href,
    SearchNote? Note = null);

/// <summary>What a search found, one list per group, each in the order to show it.</summary>
public sealed class SearchResults
{
    public static readonly SearchResults Empty = new(new Dictionary<SearchKind, IReadOnlyList<SearchItem>>());

    private readonly IReadOnlyDictionary<SearchKind, IReadOnlyList<SearchItem>> groups;

    internal SearchResults(IReadOnlyDictionary<SearchKind, IReadOnlyList<SearchItem>> groups) => this.groups = groups;

    public IReadOnlyList<SearchItem> Of(SearchKind kind) =>
        groups.TryGetValue(kind, out var items) ? items : [];

    public IReadOnlyList<SearchItem> Plants => Of(SearchKind.Plant);
    public IReadOnlyList<SearchItem> Propagations => Of(SearchKind.Propagation);
    public IReadOnlyList<SearchItem> Notes => Of(SearchKind.Note);
    public IReadOnlyList<SearchItem> Wishes => Of(SearchKind.Wish);
    public IReadOnlyList<SearchItem> Pots => Of(SearchKind.Pot);
    public IReadOnlyList<SearchItem> Mixes => Of(SearchKind.Mix);
    public IReadOnlyList<SearchItem> Feeds => Of(SearchKind.Feed);
    public IReadOnlyList<SearchItem> Products => Of(SearchKind.Product);
    public IReadOnlyList<SearchItem> TreatmentRecipes => Of(SearchKind.TreatmentRecipe);
    public IReadOnlyList<SearchItem> Places => Of(SearchKind.Place);
    public IReadOnlyList<SearchItem> Tags => Of(SearchKind.Tag);

    /// <summary>The groups that have something, in the fixed order.</summary>
    public IEnumerable<(SearchKind Kind, IReadOnlyList<SearchItem> Items)> NonEmpty =>
        Enum.GetValues<SearchKind>().Select(k => (k, Of(k))).Where(g => g.Item2.Count > 0);

    public bool IsEmpty => groups.Values.All(g => g.Count == 0);
}
