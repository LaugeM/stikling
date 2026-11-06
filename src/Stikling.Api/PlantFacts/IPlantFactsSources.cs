using Stikling.Core.PlantFacts;

namespace Stikling.Api.PlantFacts;

/// <summary>
/// What one source said. A page that isn't there is <see cref="NotFound"/>, which is an answer. A
/// source that couldn't be reached or answered with an error is <see cref="Failed"/>, which isn't
/// one and is never kept.
/// </summary>
public sealed record SourceResult<T>(T? Value, bool Failed) where T : class
{
    public static SourceResult<T> Found(T value) => new(value, false);

    public static SourceResult<T> NotFound { get; } = new(null, false);

    public static SourceResult<T> Error { get; } = new(null, true);
}

/// <summary>Finds the Wikipedia article about a genus or species. Behind an interface so the tests can use a fake.</summary>
public interface IWikipediaSource
{
    /// <param name="language">"en" or "da". Danish falls back to English when it has no article.</param>
    Task<SourceResult<WikipediaSummary>> FindAsync(string genus, string? species, string language, CancellationToken cancellationToken);
}

/// <summary>Finds Trefle's facts about a species. Behind an interface so the tests can use a fake.</summary>
public interface ITrefleSource
{
    Task<SourceResult<TrefleFacts>> FindAsync(string genus, string species, CancellationToken cancellationToken);
}
