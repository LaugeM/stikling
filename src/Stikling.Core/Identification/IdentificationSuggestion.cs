namespace Stikling.Core.Identification;

/// <summary>
/// One species a photo may show, as Pl@ntNet names it. The same shape comes back whether the app
/// asked Pl@ntNet directly or through Stikling's API.
/// </summary>
/// <param name="Score">How alike the photo is, from 0 to 1.</param>
/// <param name="CommonNames">Pl@ntNet's everyday names, in the language asked for. Empty when it has none.</param>
public sealed record IdentificationSuggestion(string Genus, string Species, double Score, IReadOnlyList<string> CommonNames);

/// <summary>What <c>POST /identify</c> answers with.</summary>
public sealed record IdentificationResponse(IReadOnlyList<IdentificationSuggestion> Suggestions);
