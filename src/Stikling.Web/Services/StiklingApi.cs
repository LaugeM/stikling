using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>The signed-in person and the collections they are in, from <c>GET /me</c>.</summary>
public sealed record Me(Guid PersonId, List<MeCollection> Collections);

/// <param name="Role">"Editor" or "Viewer".</param>
public sealed record MeCollection(Guid Id, string Name, string Role);

/// <summary>
/// Calls the sync API as whoever is signed in. Every request carries a fresh session token from
/// <see cref="AccountService"/>, which the API checks. A request that can't reach the API, or
/// that it turns away, throws <see cref="HttpRequestException"/>. Photo images are sent and
/// fetched through <see cref="PhotoService"/>, which keeps them in JavaScript.
/// </summary>
public sealed class StiklingApi(AccountSettings settings, AccountService account, PhotoService photos, DeviceFiles files) : ISyncServer, IPhotoServer
{
    private readonly HttpClient http = new() { BaseAddress = settings.ApiAddress };

    /// <summary>
    /// Asks the API to start, without waiting for it. When nobody has used it for a while it takes
    /// a few seconds to start, so this is called while Clerk loads or someone signs in, and the
    /// sync after it doesn't have to wait as long. It needs no sign-in, and never throws.
    /// </summary>
    public void Wake()
    {
        if (settings.ApiAddress is null)
            return;

        _ = WakeAsync();

        async Task WakeAsync()
        {
            try
            {
                using var response = await http.GetAsync("health");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                // Sync finds out about a server that can't be reached on its own
            }
        }
    }

    /// <summary>
    /// Who the API thinks is signed in. The first call after signing up also creates the person
    /// and their own collection on the server.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<Me> GetMeAsync()
    {
        using var request = await RequestAsync(HttpMethod.Get, "me");
        return await SendAsync<Me>(request);
    }

    /// <summary>
    /// Erases the account on the server: the collections nobody else is in, with their records and
    /// photos, and the person's settings. Asking again after it's done does no harm.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task DeleteMeAsync()
    {
        using var request = await RequestAsync(HttpMethod.Delete, "me");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Saves a ZIP of everything the server holds about the signed-in person, with what Clerk has
    /// about them added. It goes from the API to the file in JavaScript, since the photos in it
    /// can come to a lot.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task DownloadMyDataAsync(string fileName)
    {
        var body = new { account = await account.GetProfileAsync() };
        var status = await files.DownloadFromAsync(new Uri(http.BaseAddress!, "me/export"), await TokenAsync(), body, fileName);
        if (status is < 200 or >= 300)
            throw Failed(status);
    }

    public async Task<PullResponse> PullAsync(Guid collectionId, long after)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"collections/{collectionId}/records?after={after}");
        return await SendAsync<PullResponse>(request);
    }

    public async Task<PushResponse> PushAsync(Guid collectionId, IReadOnlyList<SyncRecord> records)
    {
        using var request = await RequestAsync(HttpMethod.Post, $"collections/{collectionId}/records");
        request.Content = JsonContent.Create(new PushRequest([.. records]));
        return await SendAsync<PushResponse>(request);
    }

    public async Task<JsonElement?> GetSettingsAsync()
    {
        using var request = await RequestAsync(HttpMethod.Get, "me/settings");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement?> PutSettingsAsync(JsonElement settings)
    {
        using var request = await RequestAsync(HttpMethod.Put, "me/settings");
        request.Content = JsonContent.Create(settings);
        using var response = await http.SendAsync(request);

        // The server didn't take them, e.g. dated too far ahead by a clock that is wrong
        if (response.StatusCode == HttpStatusCode.BadRequest)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<IReadOnlyList<StoredPhoto>> GetStoredAsync(Guid collectionId, IReadOnlyList<Guid> ids)
    {
        using var request = await RequestAsync(HttpMethod.Post, $"collections/{collectionId}/photos/stored");
        request.Content = JsonContent.Create(new StoredPhotosRequest([.. ids]));
        return (await SendAsync<StoredPhotosResponse>(request)).Photos;
    }

    /// <summary>How much space the collection's photos take on the server, and how much they may.</summary>
    public async Task<PhotoUsage> GetPhotoUsageAsync(Guid collectionId)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"collections/{collectionId}/photos/usage");
        return await SendAsync<PhotoUsage>(request);
    }

    // The images go between the device database and the API in photos.js, so they never pass through .NET

    public async Task<UploadOutcome> UploadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        var status = await photos.UploadAsync(ImageAddress(collectionId, photoId, size), await TokenAsync(), photoId, size == PhotoSize.Thumbnail);
        return status switch
        {
            0 => UploadOutcome.NotHere,
            >= 200 and < 300 => UploadOutcome.Uploaded,
            404 => UploadOutcome.NotYet,
            410 => UploadOutcome.Gone,
            507 => UploadOutcome.Full,
            413 or 415 => UploadOutcome.Refused,
            _ => throw Failed(status),
        };
    }

    public async Task<bool> DownloadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        var status = await photos.DownloadAsync(ImageAddress(collectionId, photoId, size), await TokenAsync(), photoId, size == PhotoSize.Thumbnail);
        return status switch
        {
            200 => true,
            404 => false,
            _ => throw Failed(status),
        };
    }

    private Uri ImageAddress(Guid collectionId, Guid photoId, PhotoSize size) =>
        new(http.BaseAddress!, $"collections/{collectionId}/photos/{photoId}/{size.PathName()}");

    private static HttpRequestException Failed(int status) =>
        status < 0
            ? new HttpRequestException("The API couldn't be reached.")
            : new HttpRequestException($"The API answered {status}.", null, (HttpStatusCode)status);

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new HttpRequestException($"The API sent an empty answer to {request.RequestUri}.");
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());
        return request;
    }

    // Answered like the API would answer a request without a sign-in
    private async Task<string> TokenAsync() =>
        await account.GetTokenAsync()
            ?? throw new HttpRequestException("Nobody is signed in.", null, HttpStatusCode.Unauthorized);
}
