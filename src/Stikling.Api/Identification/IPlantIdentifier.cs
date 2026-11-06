using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Stikling.Core.Identification;

namespace Stikling.Api.Identification;

public enum PlantNetStatus
{
    Ok,

    /// <summary>Pl@ntNet's 404: it found no species in the photo.</summary>
    NotFound,

    /// <summary>Pl@ntNet's 429: the key's quota for the day is used up.</summary>
    TooMany,

    Failed,
}

public record PlantNetAnswer(PlantNetStatus Status, IReadOnlyList<IdentificationSuggestion> Suggestions)
{
    public static PlantNetAnswer Of(PlantNetStatus status) => new(status, []);
}

/// <summary>Asks Pl@ntNet what a photo shows. Behind an interface so the tests can use a fake.</summary>
public interface IPlantIdentifier
{
    /// <param name="image">A JPEG or PNG.</param>
    /// <param name="language">"da" or "en", the language of the common names.</param>
    Task<PlantNetAnswer> IdentifyAsync(byte[] image, string contentType, string language, CancellationToken cancellationToken);
}

/// <summary>
/// Pl@ntNet's identify API. It only takes its key in the address, so the address is kept out of every
/// log: the HTTP client has no logging (see <see cref="IdentificationServices"/>), and only the status
/// Pl@ntNet answered with is logged here. The photo is sent on and not kept.
/// </summary>
public sealed class PlantNetIdentifier(HttpClient http, IOptions<IdentificationOptions> options, ILogger<PlantNetIdentifier> logger)
    : IPlantIdentifier
{
    public const string Address = "https://my-api.plantnet.org/v2/identify/all";

    public async Task<PlantNetAnswer> IdentifyAsync(byte[] image, string contentType, string language, CancellationToken cancellationToken)
    {
        var key = options.Value.PlantNetApiKey;
        if (string.IsNullOrWhiteSpace(key))
            return PlantNetAnswer.Of(PlantNetStatus.Failed);

        try
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent("auto"), "organs" },
            };
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "images", contentType == "image/png" ? "photo.png" : "photo.jpg");

            var url = $"{Address}?api-key={Uri.EscapeDataString(key)}&lang={Uri.EscapeDataString(language)}&nb-results={IdentificationRules.MaxSuggestions}&include-related-images=false";
            using var response = await http.PostAsync(url, form, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var suggestions = PlantNetResults.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    if (suggestions is null)
                    {
                        logger.LogWarning("Pl@ntNet answered 200 with something that wasn't a list of results.");
                        return PlantNetAnswer.Of(PlantNetStatus.Failed);
                    }
                    return new PlantNetAnswer(PlantNetStatus.Ok, suggestions);
                case HttpStatusCode.NotFound:
                    return PlantNetAnswer.Of(PlantNetStatus.NotFound);
                case HttpStatusCode.TooManyRequests:
                    logger.LogWarning("Pl@ntNet answered 429.");
                    return PlantNetAnswer.Of(PlantNetStatus.TooMany);
                default:
                    logger.LogWarning("Pl@ntNet answered {StatusCode}.", (int)response.StatusCode);
                    return PlantNetAnswer.Of(PlantNetStatus.Failed);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Only the kind of failure: the message of an HTTP exception can hold the address
            logger.LogWarning("Pl@ntNet couldn't be reached ({Kind}).", e.GetType().Name);
            return PlantNetAnswer.Of(PlantNetStatus.Failed);
        }
    }
}
