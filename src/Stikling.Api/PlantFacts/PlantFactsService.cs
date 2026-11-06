using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Facts = Stikling.Core.PlantFacts.PlantFacts;

namespace Stikling.Api.PlantFacts;

/// <summary>
/// Asks Wikipedia, and Trefle for a species, and keeps the answer. The cache is the server's, with
/// room for 5000 answers: one found is kept 7 days and one that found nothing 1 day, so a new article
/// shows up in time. An answer where a source failed is not kept, so the next request tries again.
/// </summary>
public sealed class PlantFactsService(
    IWikipediaSource wikipedia, ITrefleSource trefle, IOptions<PlantFactsOptions> options) : IDisposable
{
    public const int CacheSize = 5000;
    public static readonly TimeSpan FoundFor = TimeSpan.FromDays(7);
    public static readonly TimeSpan NotFoundFor = TimeSpan.FromDays(1);

    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = CacheSize });

    /// <returns>The answer, or null when Wikipedia failed and there is nothing from Trefle either.</returns>
    public async Task<Facts?> GetAsync(string genus, string? species, string language, CancellationToken cancellationToken)
    {
        var key = $"{language}|{genus}|{species}".ToLowerInvariant();
        if (cache.TryGetValue(key, out Facts? kept))
            return kept;

        var article = wikipedia.FindAsync(genus, species, language, cancellationToken);
        var data = species is not null && options.Value.TrefleOn
            ? trefle.FindAsync(genus, species, cancellationToken)
            : Task.FromResult(SourceResult<Stikling.Core.PlantFacts.TrefleFacts>.NotFound);
        await Task.WhenAll(article, data);

        var summary = article.Result;
        var facts = data.Result;
        var answer = new Facts(summary.Value, facts.Value);

        if (summary.Failed && facts.Value is null)
            return null;

        // A failed source leaves a gap that the next request may fill
        if (!summary.Failed && !facts.Failed)
        {
            var found = answer.Summary is not null || answer.Facts is not null;
            cache.Set(key, answer, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = found ? FoundFor : NotFoundFor,
            });
        }

        return answer;
    }

    public void Dispose() => cache.Dispose();
}
