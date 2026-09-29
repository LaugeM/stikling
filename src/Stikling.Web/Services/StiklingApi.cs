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
/// that it turns away, throws <see cref="HttpRequestException"/>.
/// </summary>
public sealed class StiklingApi(AccountSettings settings, AccountService account) : ISyncServer
{
    private readonly HttpClient http = new() { BaseAddress = settings.ApiAddress };

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

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new HttpRequestException($"The API sent an empty answer to {request.RequestUri}.");
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string path)
    {
        // Answered like the API would answer a request without a sign-in
        var token = await account.GetTokenAsync()
            ?? throw new HttpRequestException("Nobody is signed in.", null, HttpStatusCode.Unauthorized);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
