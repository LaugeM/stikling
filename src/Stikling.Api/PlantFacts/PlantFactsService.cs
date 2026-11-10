using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Facts = Stikling.Core.PlantFacts.PlantFacts;

namespace Stikling.Api.PlantFacts;

/// <summary>
/// Asks Wikipedia, and Trefle for a species, and keeps the answer. The cache is the server's, with
/// room for 5000 answers: one found is kept 7 days and one that found nothing 1 day, so a new article
/// shows up in time. When a source failed the answer goes out as it is, marked as incomplete so the
/// browser doesn't keep it, and the failure is remembered for 5 minutes so a source that is down
/// isn't asked again by every request.
/// </summary>
public sealed class PlantFactsService(
    IWikipediaSource wikipedia, ITrefleSource trefle, IOptions<PlantFactsOptions> options) : IDisposable
{
    public const int CacheSize = 5000;
    public static readonly TimeSpan FoundFor = TimeSpan.FromDays(7);
    public static readonly TimeSpan NotFoundFor = TimeSpan.FromDays(1);
    public static readonly TimeSpan FailedFor = TimeSpan.FromMinutes(5);

    private sealed record Entry(Facts? Answer, bool Complete);

    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = CacheSize });

    /// <returns>
    /// The answer, which is null when Wikipedia failed and there is nothing from Trefle either, and
    /// whether it is complete. A source failing makes it incomplete, and the browser shouldn't keep it.
    /// </returns>
    public async Task<(Facts? Answer, bool Complete)> GetAsync(string genus, string? species, string language, CancellationToken cancellationToken)
    {
        var key = $"{language}|{genus}|{species}".ToLowerInvariant();
        if (cache.TryGetValue(key, out Entry? kept) && kept is not null)
            return (kept.Answer, kept.Complete);

        var article = wikipedia.FindAsync(genus, species, language, cancellationToken);
        var data = species is not null && options.Value.TrefleOn
            ? trefle.FindAsync(genus, species, cancellationToken)
            : Task.FromResult(SourceResult<Stikling.Core.PlantFacts.TrefleFacts>.NotFound);
        await Task.WhenAll(article, data);

        var summary = article.Result;
        var facts = data.Result;
        var answer = new Facts(summary.Value, facts.Value);
        var complete = !summary.Failed && !facts.Failed;
        Facts? result = summary.Failed && facts.Value is null ? null : answer;

        var found = answer.Summary is not null || answer.Facts is not null;
        cache.Set(key, new Entry(result, complete), new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = !complete ? FailedFor : found ? FoundFor : NotFoundFor,
        });

        return (result, complete);
    }

    public void Dispose() => cache.Dispose();
}
