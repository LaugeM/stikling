using System.Net;
using Microsoft.Extensions.Options;
using Stikling.Core.PlantFacts;

namespace Stikling.Api.PlantFacts;

/// <summary>
/// Trefle's species data. The token goes in the address, as Trefle asks, so nothing here logs the
/// address: only status codes are logged, and the client has no request logging (see Program.cs).
/// </summary>
public sealed class TrefleSource(HttpClient http, IOptions<PlantFactsOptions> options, ILogger<TrefleSource> logger) : ITrefleSource
{
    public async Task<SourceResult<TrefleFacts>> FindAsync(string genus, string species, CancellationToken cancellationToken)
    {
        if (!options.Value.TrefleOn)
            return SourceResult<TrefleFacts>.NotFound;

        var slug = Uri.EscapeDataString(PlantFactNames.TrefleSlug(genus, species));
        var address = $"https://trefle.io/api/v1/species/{slug}?token={Uri.EscapeDataString(options.Value.TrefleToken!)}";
        try
        {
            using var response = await http.GetAsync(address, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return SourceResult<TrefleFacts>.NotFound;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Trefle answered {Status}", (int)response.StatusCode);
                return SourceResult<TrefleFacts>.Error;
            }

            var facts = TrefleParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return facts is null ? SourceResult<TrefleFacts>.NotFound : SourceResult<TrefleFacts>.Found(facts);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Trefle couldn't be reached: {Error}", e.GetType().Name);
            return SourceResult<TrefleFacts>.Error;
        }
    }
}
