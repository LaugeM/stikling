using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Stikling.Api.Auth;

/// <summary>
/// Stands in for Clerk on a developer's machine, the way Azurite stands in for Blob Storage. In
/// Development the app can sign in as a test person, and signs that person's tokens itself with
/// the key in <c>TestSignIn:SigningKey</c>. The key is only in the Development settings, and in
/// any other environment the API never sends a token here, so the hosted API turns them away.
/// </summary>
public static class TestSignIn
{
    public const string Scheme = "TestSignIn";

    /// <summary>The issuer of a test person's tokens. It is how they are told apart from Clerk's.</summary>
    public const string Issuer = "stikling-test-sign-in";

    public const string SigningKeySetting = "TestSignIn:SigningKey";

    public static IServiceCollection AddTestSignIn(this IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer(Scheme);

        services.AddOptions<JwtBearerOptions>(Scheme)
            .Configure<IHostEnvironment, IConfiguration, IOptions<AppOrigins>>((jwt, env, config, origins) =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.ValidIssuer = Issuer;
                jwt.TokenValidationParameters.ValidateAudience = false;
                jwt.TokenValidationParameters.NameClaimType = "sub";

                // Without a key nothing is accepted, should a token ever get here
                if (SigningKey(env, config) is { } key)
                    jwt.TokenValidationParameters.IssuerSigningKey = key;

                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        ClerkAuthentication.CheckAuthorizedParty(context, origins.Value);
                        return Task.CompletedTask;
                    },
                };
            });

        // Clerk's scheme hands a test person's tokens over to this one, in Development only
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IHostEnvironment, IConfiguration>((jwt, env, config) =>
            {
                if (SigningKey(env, config) is not null)
                    jwt.ForwardDefaultSelector = context => IsTestToken(context) ? Scheme : null;
            });

        return services;
    }

    private static SymmetricSecurityKey? SigningKey(IHostEnvironment env, IConfiguration config) =>
        env.IsDevelopment() && config[SigningKeySetting] is { Length: > 0 } key
            ? new SymmetricSecurityKey(Convert.FromBase64String(key))
            : null;

    private static bool IsTestToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var handler = new JsonWebTokenHandler();
        var token = header["Bearer ".Length..].Trim();
        return handler.CanReadToken(token) && handler.ReadJsonWebToken(token).Issuer == Issuer;
    }
}
