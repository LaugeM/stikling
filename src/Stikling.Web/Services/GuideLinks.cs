using System.Globalization;
using Stikling.Core.Guides;

namespace Stikling.Web.Services;

/// <summary>Addresses of the guides, and the text of a guide in the language of the app.</summary>
public static class GuideLinks
{
    public const string Index = "guides";

    /// <summary>The text with this key in Resources/Guides.resx, in the language of the app.</summary>
    public static string Text(string key) =>
        Stikling.Web.Resources.Guides.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>
    /// The address of a guide. <paramref name="from"/> says where it was opened from, so the back link can name it
    /// and the guide can open on the method that fits: "plant:{id}" or "propagation:{id}", or "pots", "soil-mixes",
    /// "propagations" or "help".
    /// </summary>
    public static string Href(string guideId, string? from = null) =>
        from is null ? $"guides/{guideId}" : $"guides/{guideId}?from={Uri.EscapeDataString(from)}";

    public static string FromPlant(Guid id) => $"plant:{id}";

    public static string FromPropagation(Guid id) => $"propagation:{id}";

    /// <summary>The guides whose name or line in the list contains the text, ignoring case and accents.</summary>
    public static IReadOnlyList<Guide> Matching(string query)
    {
        var text = query.Trim();
        if (text.Length == 0)
            return [];

        var compare = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return GuideBook.All
            .Where(g => compare.IndexOf(Text(g.TitleKey), text, loose) >= 0 || compare.IndexOf(Text(g.RowKey), text, loose) >= 0)
            .ToList();
    }
}
