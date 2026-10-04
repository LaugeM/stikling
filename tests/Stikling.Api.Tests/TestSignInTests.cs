using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Stikling.Api.Auth;

namespace Stikling.Api.Tests;

/// <summary>
/// The test person the app can sign in as in Development, with tokens it signs itself. The key
/// comes from the API's real appsettings.Development.json.
/// </summary>
[Collection(ApiCollection.Name)]
public class TestSignInTests(ApiFactory api)
{
    private string DevelopmentKey =>
        api.InEnvironment("Development").Services.GetRequiredService<IConfiguration>()[TestSignIn.SigningKeySetting]
        ?? throw new InvalidOperationException("appsettings.Development.json has no test sign-in key.");

    /// <summary>A token like the one wwwroot/js/test-account.js signs.</summary>
    private static string TestPersonToken(string key, string name = "person", string authorizedParty = ApiFactory.AppOrigin)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TestSignIn.Issuer,
            Claims = new Dictionary<string, object> { ["sub"] = $"test_{name}", ["azp"] = authorizedParty },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(key)), SecurityAlgorithms.HmacSha256),
        });
    }

    private static async Task<HttpStatusCode> GetMeWith(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync("/me")).StatusCode;
    }

    private static string NewName() => Guid.NewGuid().ToString("N");

    [Fact]
    public async Task In_development_a_test_person_can_sign_in()
    {
        var token = TestPersonToken(DevelopmentKey, NewName());

        Assert.Equal(HttpStatusCode.OK, await GetMeWith(api.InEnvironment("Development"), token));
    }

    [Fact]
    public async Task Clerk_tokens_still_work_in_development()
    {
        var token = api.TokenFor(ApiFactory.NewClerkUserId());

        Assert.Equal(HttpStatusCode.OK, await GetMeWith(api.InEnvironment("Development"), token));
    }

    [Fact]
    public async Task In_development_a_test_token_signed_with_another_key_is_turned_away()
    {
        var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.InEnvironment("Development"), TestPersonToken(otherKey, NewName())));
    }

    [Fact]
    public async Task In_development_a_test_token_issued_to_another_site_is_turned_away()
    {
        var token = TestPersonToken(DevelopmentKey, NewName(), authorizedParty: "https://example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.InEnvironment("Development"), token));
    }

    [Fact]
    public async Task The_hosted_configuration_turns_a_test_person_away()
    {
        var token = TestPersonToken(DevelopmentKey, NewName());

        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(api.InEnvironment("Production"), token));
    }

    [Fact]
    public async Task The_hosted_api_ignores_a_test_key_even_if_one_is_set()
    {
        var key = DevelopmentKey;
        var hosted = api.InEnvironment("Production", new() { [TestSignIn.SigningKeySetting] = key });

        Assert.Equal(HttpStatusCode.Unauthorized, await GetMeWith(hosted, TestPersonToken(key, NewName())));
    }
}
