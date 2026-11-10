using System.Text.Json;

namespace Stikling.Core.PlantFacts;

/// <summary>
/// Reads Trefle's species answer. It is lenient: anything missing or unreadable is left out. Only the
/// height, the bloom months, the growth habit and the sources are read. Never edibility, toxicity,
/// light, humidity, soil or anything else that sounds like care, which Trefle's own issues call unreliable.
/// </summary>
public static class TrefleParser
{
    /// <summary>The growth habits shown, in the USDA's words. Any other value is left out.</summary>
    public static readonly IReadOnlyList<string> GrowthHabits = ["Tree", "Shrub", "Subshrub", "Vine", "Forb/herb"];

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>The facts, or null when there are no height, bloom months or growth habit to show.</summary>
    public static TrefleFacts? Parse(string json)
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
            if (!Object(document.RootElement, "data", out var data))
                return null;

            int? height = null;
            string? habit = null;
            if (Object(data, "specifications", out var specifications))
            {
                if (Object(specifications, "maximum_height", out var maximum)
                    && maximum.TryGetProperty("cm", out var cm) && cm.ValueKind == JsonValueKind.Number
                    && cm.TryGetDouble(out var cmValue) && cmValue is > 0 and < 100_000)
                    height = (int)Math.Round(cmValue);

                if (specifications.TryGetProperty("growth_habit", out var growth) && growth.ValueKind == JsonValueKind.String)
                    habit = GrowthHabits.FirstOrDefault(h => string.Equals(h, growth.GetString()?.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            var bloom = new SortedSet<int>();
            if (Object(data, "growth", out var growthBlock)
                && growthBlock.TryGetProperty("bloom_months", out var months) && months.ValueKind == JsonValueKind.Array)
            {
                foreach (var month in months.EnumerateArray())
                {
                    if (month.ValueKind != JsonValueKind.String || month.GetString() is not { Length: >= 3 } name)
                        continue;
                    var index = Array.IndexOf(Months, name[..3].ToLowerInvariant());
                    if (index >= 0)
                        bloom.Add(index + 1);
                }
            }

            if (height is null && bloom.Count == 0 && habit is null)
                return null;

            var sources = new List<FactSource>();
            if (data.TryGetProperty("sources", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var source in list.EnumerateArray())
                {
                    if (source.ValueKind != JsonValueKind.Object
                        || !source.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(name.GetString()))
                        continue;

                    string? url = null;
                    if (source.TryGetProperty("url", out var link) && link.ValueKind == JsonValueKind.String
                        && Uri.TryCreate(link.GetString(), UriKind.Absolute, out var address)
                        && address.Scheme is "http" or "https")
                        url = address.ToString();

                    var text = name.GetString()!.Trim();
                    if (!sources.Any(s => s.Name == text && s.Url == url))
                        sources.Add(new FactSource(text, url));
                }
            }

            return new TrefleFacts(height, [.. bloom], habit, sources);
        }
    }

    private static bool Object(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Object;
    }
}
