using System.Text.Json.Serialization;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Compare;

/// <summary>What a friend has to swap, from the plant itself. Cuttings is never stored, but the JSON is text like the other enums.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FriendSpare>))]
public enum FriendSpare
{
    None,

    /// <summary>Cuttings that exist, with <see cref="FriendPlant.SpareCount"/> and <see cref="FriendPlant.Stage"/>.</summary>
    Cuttings,
    CuttingsToSpare,
    UpForSwap
}

/// <summary>A plant or cutting on a friend's side of a comparison. Id and Cover are only known from a page.</summary>
/// <param name="Name">The name as the friend shows it. The botanical parts are only set when it has a genus.</param>
public sealed record FriendPlant(
    Guid? Id,
    SubjectType Type,
    string Name,
    string? Genus,
    string? Species,
    string? Cultivar,
    FriendSpare Spare,
    int SpareCount,
    PropagationStage? Stage,
    SharePhoto? Cover)
{
    /// <summary>"Can spare 2 rooted cuttings", "Cuttings to spare", "Up for swap", or null when nothing is spare.</summary>
    public string? SpareText() => Spare switch
    {
        FriendSpare.Cuttings => Stage is { } stage
            ? F(SpareCount == 1 ? Strings.Compare.CanSpare_One : Strings.Compare.CanSpare_Other, SpareCount, SwapLists.StageText(stage))
            : Plural.Of(SpareCount, Strings.Compare.CanSpareNoStage_One, Strings.Compare.CanSpareNoStage_Other),
        FriendSpare.CuttingsToSpare => SwapLists.MarkText(SpareMark.CuttingsToSpare),
        FriendSpare.UpForSwap => SwapLists.MarkText(SpareMark.UpForSwap),
        _ => null
    };
}

/// <summary>A plant a friend is looking for. Only its name, never notes or a price.</summary>
public sealed record FriendWish(string Name, string? Genus, string? Species, string? Cultivar);

/// <summary>What <c>GET /s/{address}/compare</c> answers: the friend's list and where each cover can be fetched.</summary>
/// <param name="Address">The page's address, @name in lower case or its token.</param>
/// <param name="CoverUrls">The full address of each cover photo's image, by the photo's id.</param>
public sealed record FriendPageResponse(FriendList List, string Address, IReadOnlyDictionary<Guid, string> CoverUrls);

/// <summary>A friend's side of a comparison, from a public page or a pasted list. Nothing of it is stored.</summary>
/// <param name="Title">The page's title, or null for a pasted text.</param>
/// <param name="WishlistShared">False when the page has its wishlist off, or the pasted text was a plant list.</param>
/// <param name="SwapContact">A link or an address to ask for a swap. Only set when the page owner turned swap requests on.</param>
/// <param name="UnreadLines">Lines of a pasted text that couldn't be read as a name.</param>
public sealed record FriendList(
    string? Title,
    IReadOnlyList<FriendPlant> Plants,
    IReadOnlyList<FriendWish> Wishes,
    bool WishlistShared,
    string? SwapContact,
    IReadOnlyList<string> UnreadLines);
