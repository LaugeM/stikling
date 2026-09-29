using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Stikling.Api.Data;
using Testcontainers.MsSql;

namespace Stikling.Api.Tests;

/// <summary>
/// Runs the API against a real SQL Server in Docker. Tokens are signed with a key made here
/// instead of Clerk's, and everything else about checking them is the same as in production.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Issuer = "https://clerk.stikling.test";
    public const string AppOrigin = "http://localhost:5170";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public SecurityKey SigningKey { get; } = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test" };

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StiklingDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Stikling", _sql.GetConnectionString());
        builder.UseSetting("Clerk:Authority", Issuer);
        builder.UseSetting("AppOrigins:0", AppOrigin);

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                // Stands in for the OpenID configuration the API would fetch from Clerk
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(SigningKey);
                jwt.Configuration = configuration;
                jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            }));
    }

    /// <summary>A session token shaped like Clerk's, valid unless a parameter says otherwise.</summary>
    public string TokenFor(
        string clerkUserId,
        string? authorizedParty = AppOrigin,
        string issuer = Issuer,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var claims = new Dictionary<string, object> { ["sub"] = clerkUserId };
        if (authorizedParty is not null)
            claims["azp"] = authorizedParty;

        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(1);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Claims = claims,
            IssuedAt = expiresAt.AddMinutes(-1),
            NotBefore = expiresAt.AddMinutes(-1),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }

    public HttpClient ClientFor(string clerkUserId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(clerkUserId));
        return client;
    }

    public static string NewClerkUserId() => $"user_{Guid.NewGuid():N}";

    public async Task WithDbAsync(Func<StiklingDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<StiklingDbContext>());
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
