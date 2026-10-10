using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Stikling.Core.Sharing;
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
public sealed class StiklingApi(AccountSettings settings, AccountService account, PhotoService photos, DeviceFiles files) : ISyncServer, IPhotoServer, IShareLinkServer
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

    // The share links. A call that fails comes back with the problem's own words instead of throwing,
    // since they are shown on the Share sheet as they are.

    public async Task<ShareOutcome<IReadOnlyList<ShareLinkInfo>>> ListAsync(Guid collectionId) =>
        await ShareCallAsync<List<ShareLinkInfo>, IReadOnlyList<ShareLinkInfo>>(HttpMethod.Get, $"collections/{collectionId}/shares", null, list => list);

    public async Task<ShareOutcome<ShareLinkInfo>> CreateAsync(Guid collectionId, CreateShareLinkRequest request) =>
        await ShareCallAsync<ShareLinkInfo, ShareLinkInfo>(HttpMethod.Post, $"collections/{collectionId}/shares", request, link => link);

    public async Task<ShareOutcome<ShareLinkInfo>> UpdateAsync(Guid collectionId, Guid id, UpdateShareLinkRequest request) =>
        await ShareCallAsync<ShareLinkInfo, ShareLinkInfo>(HttpMethod.Put, $"collections/{collectionId}/shares/{id}", request, link => link);

    public async Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId, Guid id) =>
        await ShareCallAsync<object, bool>(HttpMethod.Delete, $"collections/{collectionId}/shares/{id}", null, _ => true, hasBody: false);

    private async Task<ShareOutcome<TResult>> ShareCallAsync<TBody, TResult>(
        HttpMethod method, string path, object? body, Func<TBody, TResult> result, bool hasBody = true)
    {
        try
        {
            using var request = await RequestAsync(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType());
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return ShareOutcome<TResult>.Failure(await ProblemOfAsync(response));

            if (!hasBody)
                return ShareOutcome<TResult>.Success(result(default!));
            return await response.Content.ReadFromJsonAsync<TBody>() is { } value
                ? ShareOutcome<TResult>.Success(result(value))
                : ShareOutcome<TResult>.Failure("The server sent an empty answer.");
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ShareOutcome<TResult>.Failure("Your sign-in has run out. Sign out and in again to carry on.");
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            return ShareOutcome<TResult>.Failure("The Stikling server can't be reached right now.", offline: true);
        }
    }

    // The API explains a refusal in "detail", or for a field that is too long in "errors"
    private static async Task<string> ProblemOfAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && detail.GetString() is { Length: > 0 } text)
                return text;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array && field.Value.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.String } first)
                        return first.GetString()!;
                }
            }
        }
        catch (Exception e) when (e is JsonException or HttpRequestException)
        {
            // Falls through to the plain words
        }

        return response.StatusCode == HttpStatusCode.NotFound
            ? "That link isn't there any more. Close this and open Share again."
            : "The server turned that down. Try again in a moment.";
    }

    // The images go between the device database and the API in photos.js, so they never pass through .NET

    public async Task<UploadOutcome> UploadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        var status = await UntilNotTooManyAsync(async () =>
            await photos.UploadAsync(ImageAddress(collectionId, photoId, size), await TokenAsync(), photoId, size == PhotoSize.Thumbnail));
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
        var status = await UntilNotTooManyAsync(async () =>
            await photos.DownloadAsync(ImageAddress(collectionId, photoId, size), await TokenAsync(), photoId, size == PhotoSize.Thumbnail));
        return status switch
        {
            200 => true,
            404 => false,
            _ => throw Failed(status),
        };
    }

    /// <summary>How long a photo waits when the server says this account has sent too much at once.</summary>
    private static readonly TimeSpan TooManyWait = TimeSpan.FromSeconds(10);

    /// <summary>How many times it is sent before the sync gives up until next time.</summary>
    private const int TooManyTries = 30;

    // A first sync with a lot of photos can go past what the server takes from one account at
    // once (RequestLimits in the API). It answers 429, and the photos carry on more slowly.
    private static async Task<int> UntilNotTooManyAsync(Func<Task<int>> send)
    {
        var status = await send();
        for (var tries = 1; status == 429 && tries < TooManyTries; tries++)
        {
            await Task.Delay(TooManyWait);
            status = await send();
        }
        return status;
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
