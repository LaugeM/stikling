using Stikling.Core.Sharing;

namespace Stikling.Api.Sharing;

/// <summary>The piece of the path a part is at.</summary>
public static class PublicPagePaths
{
    public static string Segment(PublicPartKind kind) => kind switch
    {
        PublicPartKind.Plants => "plants",
        PublicPartKind.Cuttings => "cuttings",
        PublicPartKind.Swap => "swap",
        _ => "wishlist"
    };
}
