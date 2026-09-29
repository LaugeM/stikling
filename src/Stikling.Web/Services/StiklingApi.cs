using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Stikling.Web.Services;

/// <summary>The signed-in person and the collections they are in, from <c>GET /me</c>.</summary>
public sealed record Me(Guid PersonId, List<MeCollection> Collections);

/// <param name="Role">"Editor" or "Viewer".</param>
public sealed record MeCollection(Guid Id, string Name, string Role);

/// <summary>
/// Calls the sync API as whoever is signed in. Every request carries a fresh session token from
/// <see cref="AccountService"/>, which the API checks.
/// </summary>
public sealed class StiklingApi(AccountSettings settings, AccountService account)
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
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Me>()
            ?? throw new HttpRequestException("The API sent an empty answer to /me.");
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string path)
    {
        var token = await account.GetTokenAsync()
            ?? throw new InvalidOperationException("Nobody is signed in.");

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
