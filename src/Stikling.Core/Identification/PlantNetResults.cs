using System.Text.Json;

namespace Stikling.Core.Identification;

/// <summary>Reads Pl@ntNet's answer into <see cref="IdentificationSuggestion"/>s. The one place that knows its shape.</summary>
public static class PlantNetResults
{
    /// <summary>
    /// The suggestions in Pl@ntNet's JSON, best first and at most <see cref="IdentificationRules.MaxSuggestions"/>.
    /// Items whose name doesn't split into a genus and a species are skipped. Null when the text isn't
    /// an answer from Pl@ntNet at all.
    /// </summary>
    public static IReadOnlyList<IdentificationSuggestion>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement is not { ValueKind: JsonValueKind.Object } root
                || !root.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return null;

            var found = new List<IdentificationSuggestion>();
            foreach (var item in results.EnumerateArray())
                if (Map(item) is { } suggestion)
                    found.Add(suggestion);

            return found.OrderByDescending(s => s.Score).Take(IdentificationRules.MaxSuggestions).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>One item of <c>results</c>, or null when it can't be read.</summary>
    public static IdentificationSuggestion? Map(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("species", out var species) || species.ValueKind != JsonValueKind.Object
            || Text(species, "scientificNameWithoutAuthor") is not { } full
            || !species.TryGetProperty("genus", out var genusElement) || genusElement.ValueKind != JsonValueKind.Object
            || Text(genusElement, "scientificNameWithoutAuthor") is not { } genus)
            return null;

        // "Monstera deliciosa" without the genus in front. A hybrid keeps its ×: "Mentha × piperita" gives "× piperita".
        var prefix = genus + " ";
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        var name = full[prefix.Length..].Trim();
        if (name.Length == 0)
            return null;

        var score = item.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetDouble() : 0;

        var names = new List<string>();
        if (species.TryGetProperty("commonNames", out var common) && common.ValueKind == JsonValueKind.Array)
            foreach (var n in common.EnumerateArray())
                if (n.ValueKind == JsonValueKind.String && n.GetString() is { Length: > 0 } text)
                    names.Add(text);

        return new IdentificationSuggestion(genus, name, score, names);
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()?.Trim() is { Length: > 0 } text
            ? text
            : null;
}
