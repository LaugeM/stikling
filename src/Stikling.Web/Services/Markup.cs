using Microsoft.AspNetCore.Components;
using static Stikling.Core.Text.Format;

namespace Stikling.Web.Services;

/// <summary>Helpers for resource texts that have markup or an element inside them.</summary>
public static class Markup
{
    public static string H(string text) => System.Net.WebUtility.HtmlEncode(text);

    public static string Link(string href, string text, string? cssClass = null) =>
        $"<a{(cssClass is null ? "" : $" class=\"{cssClass}\"")} href=\"{href}\">{H(text)}</a>";

    // Resource text with already encoded pieces (links, names) where {0}, {1} and so on are
    public static MarkupString Html(string format, params string[] encoded) => (MarkupString)F(format, encoded);

    // A sentence with an element inside it: the element goes where {0} is
    public static RenderFragment Around(string format, RenderFragment inner) => builder =>
    {
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        builder.AddContent(0, format[..at]);
        builder.AddContent(1, inner);
        builder.AddContent(2, format[(at + 3)..]);
    };
}
