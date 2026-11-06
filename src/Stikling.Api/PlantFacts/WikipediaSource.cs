using System.Net;
using Stikling.Core.PlantFacts;

namespace Stikling.Api.PlantFacts;

/// <summary>Wikipedia's page summaries, from the REST API. It needs no key, but asks for a User-Agent that says who is asking.</summary>
public sealed class WikipediaSource(HttpClient http, ILogger<WikipediaSource> logger) : IWikipediaSource
{
    public const string UserAgent = "Stikling/1.0 (https://stikling.app)";

    public async Task<SourceResult<WikipediaSummary>> FindAsync(
        string genus, string? species, string language, CancellationToken cancellationToken)
    {
        var first = await FindInAsync(genus, species, language, cancellationToken);
        if (first.Value is not null || language == "en")
            return first;

        // Danish has far fewer articles. A Danish error doesn't hide an English article, but if the
        // English one isn't there either, the answer is still that something went wrong
        var english = await FindInAsync(genus, species, "en", cancellationToken);
        return english.Value is null && first.Failed ? SourceResult<WikipediaSummary>.Error : english;
    }

    private async Task<SourceResult<WikipediaSummary>> FindInAsync(
        string genus, string? species, string language, CancellationToken cancellationToken)
    {
        var title = Uri.EscapeDataString(PlantFactNames.WikipediaTitle(genus, species));
        try
        {
            using var response = await http.GetAsync($"https://{language}.wikipedia.org/api/rest_v1/page/summary/{title}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return SourceResult<WikipediaSummary>.NotFound;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Wikipedia ({Language}) answered {Status}", language, (int)response.StatusCode);
                return SourceResult<WikipediaSummary>.Error;
            }

            var summary = WikipediaParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken), language, genusOnly: species is null);
            return summary is null ? SourceResult<WikipediaSummary>.NotFound : SourceResult<WikipediaSummary>.Found(summary);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Wikipedia ({Language}) couldn't be reached: {Error}", language, e.GetType().Name);
            return SourceResult<WikipediaSummary>.Error;
        }
    }
}
