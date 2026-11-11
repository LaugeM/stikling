using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Compare;

/// <summary>How well two names match.</summary>
public enum MatchKind
{
    /// <summary>The same genus, species and cultivar, or the same name typed in.</summary>
    Exact,

    /// <summary>The same species, but another cultivar, or a cultivar on one side only.</summary>
    Close
}

/// <summary>One of their plants that is on my wishlist.</summary>
/// <param name="Difference">For a close match, what theirs is, like "Theirs: 'Albo'".</param>
public sealed record ForYouMatch(FriendPlant Theirs, Wish Mine, MatchKind Kind, string? Difference);

/// <summary>One of my plants or cuttings that is on their wishlist.</summary>
/// <param name="Difference">For a close match, what mine is, like "Yours: 'Albo'".</param>
/// <param name="IsSpare">Mine is up for swap, or is a cutting with some to spare.</param>
/// <param name="CanMarkUpForSwap">Mine is a plant that isn't offered yet, so the app can offer to mark it.</param>
public sealed record ForThemMatch(
    Guid Id,
    SubjectType Type,
    string Name,
    Guid? CoverPhotoId,
    FriendWish Theirs,
    MatchKind Kind,
    string? Difference,
    bool IsSpare,
    bool CanMarkUpForSwap);

/// <param name="ForYou">Their plants that are on my wishlist.</param>
/// <param name="ForThem">My plants that are on their wishlist.</param>
/// <param name="More">The rest of their plants.</param>
public sealed record ListComparisonResult(
    IReadOnlyList<ForYouMatch> ForYou,
    IReadOnlyList<ForThemMatch> ForThem,
    IReadOnlyList<FriendPlant> More);

/// <summary>
/// Compares a friend's list with my wishlist and my plants. Names are compared the way
/// <see cref="Wishlist.WishMatches"/> does it. Nothing is stored.
/// </summary>
public static class ListComparison
{
    // What is known of a name: the key of its botanical parts, and the name typed in when it has no genus
    private readonly record struct Side((string Genus, string Species, string Cultivar) Key, string? Text);

    private static Side Of(string? genus, string? species, string? cultivar, string? text, PlantDictionary? dictionary) =>
        new(PlantNameKey.Of(genus, species, cultivar, dictionary), PlantNameKey.Clean(text));

    /// <summary>
    /// Whether <paramref name="have"/> is what <paramref name="wanted"/> asks for. A wanted name with a
    /// genus is compared by genus, species and cultivar. One with no genus is compared on the name it
    /// was typed as, since a lone species or cultivar says too little. A name with nothing in it matches nothing.
    /// </summary>
    private static MatchKind? Match(Side wanted, Side have)
    {
        if (wanted.Key.Genus.Length > 0)
        {
            if (have.Key.Genus.Length == 0 || wanted.Key.Genus != have.Key.Genus)
                return null;
            if (wanted.Key == have.Key)
                return MatchKind.Exact;
            return wanted.Key.Species.Length > 0 && wanted.Key.Species == have.Key.Species ? MatchKind.Close : null;
        }

        return wanted.Text is { } text && text == have.Text ? MatchKind.Exact : null;
    }

    public static ListComparisonResult Compare(
        FriendList friend, IEnumerable<Wish> myWishes, IEnumerable<Plant> myPlants, IEnumerable<Propagation> myPropagations, PlantDictionary? dictionary)
    {
        var wishes = myWishes.Where(w => !w.IsDeleted && !w.IsGot)
            .Select(w => (Wish: w, Side: Of(w.Genus, w.KnownSpecies, w.Cultivar, w.Name, dictionary))).ToList();
        var plants = myPlants.Where(p => !p.IsDeleted && p.InCollection && !ExampleFamily.PlantIds.Contains(p.Id))
            .Select(p => (Plant: p, Side: Of(p.Genus, p.KnownSpecies, p.Cultivar, p.Nickname, dictionary))).ToList();
        var propagations = myPropagations.Where(p => !p.IsDeleted && p.IsActive && !ExampleFamily.PropagationIds.Contains(p.Id))
            .Select(p => (Propagation: p, Side: Of(p.Genus, p.KnownSpecies, p.Cultivar, p.Nickname, dictionary))).ToList();

        // A plant with neither a genus nor a name can't be matched on anything
        bool Unnamed(Side side) => side.Key.Genus.Length == 0 && side.Text is null;

        // Their plants on my wishlist, each against the first wish that fits, an exact one before a close one
        var forYou = new List<(ForYouMatch Match, int Index)>();
        var matchedTheirs = new HashSet<FriendPlant>();
        for (var i = 0; i < friend.Plants.Count; i++)
        {
            var theirs = friend.Plants[i];
            var side = Of(theirs.Genus, theirs.Species, theirs.Cultivar, theirs.Name, dictionary);
            if (Unnamed(side) || IsUnnamedText(theirs, side))
                continue;

            var found = wishes
                .Select(w => (w.Wish, Kind: Match(w.Side, side)))
                .Where(m => m.Kind is not null)
                .OrderBy(m => m.Kind)
                .FirstOrDefault();
            if (found.Wish is null)
                continue;

            var kind = found.Kind!.Value;
            var difference = kind == MatchKind.Close
                ? Describe(Strings.Compare.DifferenceTheirs, Strings.Compare.DifferenceTheirsNone, theirs.Cultivar)
                : null;
            forYou.Add((new ForYouMatch(theirs, found.Wish, kind, difference), i));
            matchedTheirs.Add(theirs);
        }
        var forYouSorted = forYou
            .OrderBy(m => m.Match.Theirs.Spare != FriendSpare.None ? 0 : 1 + (int)m.Match.Kind)
            .ThenBy(m => m.Index)
            .Select(m => m.Match)
            .ToList();

        // My plants on their wishlist, one row for each of their wishes
        var forThem = new List<(ForThemMatch Match, int Index)>();
        for (var i = 0; i < friend.Wishes.Count; i++)
        {
            var theirs = friend.Wishes[i];
            var side = Of(theirs.Genus, theirs.Species, theirs.Cultivar, theirs.Name, dictionary);
            if (Unnamed(side))
                continue;

            var candidates = plants
                .Select(p => (Id: p.Plant.Id, Type: SubjectType.Plant, Name: p.Plant.DisplayName, Cover: p.Plant.CoverPhotoId,
                    Cultivar: p.Plant.Cultivar, Spare: p.Plant.IsOffered, CanMark: !p.Plant.IsOffered, Kind: Match(side, p.Side)))
                .Concat(propagations.Select(p => (Id: p.Propagation.Id, Type: SubjectType.Propagation, Name: p.Propagation.DisplayName, Cover: p.Propagation.CoverPhotoId,
                    Cultivar: p.Propagation.Cultivar, Spare: p.Propagation.SpareAvailable > 0, CanMark: false, Kind: Match(side, p.Side))))
                .Where(c => c.Kind is not null)
                .ToList();
            if (candidates.Count == 0)
                continue;

            var best = candidates.Min(c => c.Kind);
            var pick = candidates.Where(c => c.Kind == best).OrderByDescending(c => c.Spare).First();
            var kind = pick.Kind!.Value;
            var difference = kind == MatchKind.Close
                ? Describe(Strings.Compare.DifferenceYours, Strings.Compare.DifferenceYoursNone, pick.Cultivar)
                : null;
            forThem.Add((new ForThemMatch(pick.Id, pick.Type, pick.Name, pick.Cover, theirs, kind, difference, pick.Spare, pick.CanMark), i));
        }
        var forThemSorted = forThem
            .OrderBy(m => m.Match.IsSpare ? 0 : 1)
            .ThenBy(m => (int)m.Match.Kind)
            .ThenBy(m => m.Index)
            .Select(m => m.Match)
            .ToList();

        // The rest of theirs, leaving out what I have and what is already above
        var more = new List<(FriendPlant Plant, int Index)>();
        for (var i = 0; i < friend.Plants.Count; i++)
        {
            var theirs = friend.Plants[i];
            if (matchedTheirs.Contains(theirs))
                continue;
            var side = Of(theirs.Genus, theirs.Species, theirs.Cultivar, theirs.Name, dictionary);
            var have = !Unnamed(side) && plants.Any(p => Match(side, p.Side) == MatchKind.Exact);
            if (have)
                continue;
            more.Add((theirs, i));
        }
        var moreSorted = more.OrderBy(m => m.Plant.Spare != FriendSpare.None ? 0 : 1).ThenBy(m => m.Index).Select(m => m.Plant).ToList();

        return new ListComparisonResult(forYouSorted, forThemSorted, moreSorted);
    }

    // A page's plant with no genus and no nickname is shown as "Unnamed plant", which says nothing to match on
    private static bool IsUnnamedText(FriendPlant plant, Side side) =>
        side.Key.Genus.Length == 0 && plant.Name.Trim().Equals(Strings.Shared.SwapUnnamedPlant, StringComparison.CurrentCultureIgnoreCase);

    private static string Describe(string withCultivar, string without, string? cultivar) =>
        PlantNames.Cultivar(cultivar) is { } c ? F(withCultivar, $"'{c}'") : without;

    /// <summary>"3 for you · 2 for them".</summary>
    public static string CountsLine(ListComparisonResult result) =>
        string.Join(" · ",
            Plural.Of(result.ForYou.Count, Strings.Compare.ForYouCount_One, Strings.Compare.ForYouCount_Other),
            Plural.Of(result.ForThem.Count, Strings.Compare.ForThemCount_One, Strings.Compare.ForThemCount_Other));
}
