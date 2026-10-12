using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>
/// Fetches what a public share link holds, for someone keeping a copy of a plant's history: the
/// frozen copy, and its photos (through <see cref="PhotoService"/>, so the images stay in
/// JavaScript). These endpoints are public, so nothing is signed.
/// </summary>
public sealed class SharedCopies(AccountSettings settings, PhotoService photos) : IShareCopySource, IReceivedPhotos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient http = new() { BaseAddress = settings.ApiAddress };

    public async Task<ShareCopyOutcome> GetAsync(string token)
    {
        if (settings.ApiAddress is null)
            return new(ShareCopyStatus.Unreachable, null);

        try
        {
            using var response = await http.GetAsync($"s/{Uri.EscapeDataString(token)}/copy");
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return new(ShareCopyStatus.NotFound, null);
                case HttpStatusCode.Gone:
                    return new(ShareCopyStatus.Gone, null);
                case HttpStatusCode.OK:
                    var copy = await response.Content.ReadFromJsonAsync<ShareCopy>(Json);
                    // A newer format needs a newer app, so it reads like a server that can't be reached
                    return copy is null || copy.Version > ShareCopy.CurrentVersion
                        ? new(ShareCopyStatus.Unreachable, null)
                        : new(ShareCopyStatus.Ok, copy);
                default:
                    return new(ShareCopyStatus.Unreachable, null);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return new(ShareCopyStatus.Unreachable, null);
        }
    }

    public async Task<bool> FetchAsync(string token, Guid giverPhotoId, Guid storeAs)
    {
        if (settings.ApiAddress is null)
            return false;

        var address = new Uri(settings.ApiAddress,
            $"s/{Uri.EscapeDataString(token)}/photos/{giverPhotoId}/{PhotoSize.Full.PathName()}");
        return await photos.FetchSharedAsync(address, storeAs);
    }
}
