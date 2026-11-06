using System.Text.Json;

namespace Stikling.Core.PlantFacts;

/// <summary>Reads Wikipedia's page summary (the REST API's /page/summary/{title}).</summary>
public static class WikipediaParser
{
    /// <summary>
    /// The summary, or null when the page is no use: not readable, a disambiguation page, with no text,
    /// or, for a genus on its own, a page that isn't about a genus or a plant (a genus name can also be
    /// a place or a person).
    /// </summary>
    public static WikipediaSummary? Parse(string json, string language, bool genusOnly)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (Text(root, "type") == "disambiguation")
                return null;

            var extract = Text(root, "extract")?.Trim();
            var title = Text(root, "title")?.Trim();
            if (string.IsNullOrEmpty(extract) || string.IsNullOrEmpty(title))
                return null;

            if (genusOnly)
            {
                var words = language == "da" ? new[] { "slægt", "plante" } : ["genus", "plant"];
                var about = $"{Text(root, "description")} {extract}";
                if (!words.Any(w => about.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    return null;
            }

            string? page = null;
            if (root.TryGetProperty("content_urls", out var urls) && urls.ValueKind == JsonValueKind.Object
                && urls.TryGetProperty("desktop", out var desktop) && desktop.ValueKind == JsonValueKind.Object)
                page = Text(desktop, "page");

            // Only a Wikipedia address is linked to
            if (!Uri.TryCreate(page, UriKind.Absolute, out var address)
                || address.Scheme != Uri.UriSchemeHttps
                || !address.Host.EndsWith(".wikipedia.org", StringComparison.Ordinal))
                return null;

            return new WikipediaSummary(extract, language, title, address.ToString());
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
