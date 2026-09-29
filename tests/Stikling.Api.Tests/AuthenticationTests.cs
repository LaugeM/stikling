using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthenticationTests(ApiFactory api)
{
    private async Task<HttpStatusCode> GetMeWith(string? token)
    {
        var client = api.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (await client.GetAsync("/me")).StatusCode;
    }

    [Fact]
    public async Task A_valid_token_is_accepted()
    {
        Assert.Equal(HttpStatusCode.OK, await GetMeWith(api.TokenFor(ApiFactory.NewClerkUserId())));
    }

    [Fact]
    public async Task No_token_is_turned_away()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(null));
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_turned_away()
    {
        var otherKey = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test" };
        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.TokenFor(ApiFactory.NewClerkUserId(), signingKey: otherKey)));
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_turned_away()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.TokenFor(ApiFactory.NewClerkUserId(), issuer: "https://someone-else.clerk.accounts.dev")));
    }

    [Fact]
    public async Task An_expired_token_is_turned_away()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.TokenFor(ApiFactory.NewClerkUserId(), expires: DateTime.UtcNow.AddMinutes(-10))));
    }

    [Fact]
    public async Task A_token_issued_to_another_site_is_turned_away()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.TokenFor(ApiFactory.NewClerkUserId(), authorizedParty: "https://example.com")));
    }

    [Fact]
    public async Task A_token_without_an_authorized_party_is_accepted()
    {
        // Clerk only sets azp for requests made from a browser, so its docs check it when present
        Assert.Equal(HttpStatusCode.OK, await GetMeWith(api.TokenFor(ApiFactory.NewClerkUserId(), authorizedParty: null)));
    }

    [Fact]
    public async Task Health_needs_no_token()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_app_may_call_the_api_from_its_own_origin()
    {
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/me");
        preflight.Headers.Add("Origin", ApiFactory.AppOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await api.CreateClient().SendAsync(preflight);

        Assert.Equal(ApiFactory.AppOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Other_sites_may_not_call_the_api()
    {
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/me");
        preflight.Headers.Add("Origin", "https://example.com");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await api.CreateClient().SendAsync(preflight);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
