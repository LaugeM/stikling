namespace Stikling.Core.PlantFacts;

/// <summary>What the plant facts card shows about a species or genus. Either part can be missing.</summary>
public sealed record PlantFacts(WikipediaSummary? Summary, TrefleFacts? Facts);

/// <summary>The opening of a Wikipedia article, as plain text.</summary>
/// <param name="Language">"en" or "da": the language of the article, which isn't the one asked for when Danish had none.</param>
/// <param name="PageUrl">The article, to link to it.</param>
public sealed record WikipediaSummary(string Text, string Language, string Title, string PageUrl);

/// <summary>
/// A few facts from Trefle. Only what describes the plant: nothing about care, edibility or
/// toxicity is ever read.
/// </summary>
/// <param name="GrowthHabit">One of <see cref="TrefleParser.GrowthHabits"/>, as Trefle writes it.</param>
/// <param name="BloomMonths">1 to 12, in order.</param>
public sealed record TrefleFacts(
    int? MaxHeightCm,
    IReadOnlyList<int> BloomMonths,
    string? GrowthHabit,
    IReadOnlyList<FactSource> Sources);

public sealed record FactSource(string Name, string? Url);
