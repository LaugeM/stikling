using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Stikling.Api.Auth;

/// <summary>
/// Checks Clerk's session tokens with ASP.NET Core's own JWT bearer authentication. Clerk
/// publishes an OpenID configuration at its Frontend API URL, so the issuer and signing keys
/// come from there, and the only Clerk-specific part is the <c>azp</c> check.
/// </summary>
public static class ClerkAuthentication
{
    /// <summary>The origin the token was issued to. Clerk's docs say to check it.</summary>
    public const string AuthorizedPartyClaim = "azp";

    public static IServiceCollection AddClerkAuthentication(this IServiceCollection services)
    {
        services.AddOptions<ClerkOptions>()
            .BindConfiguration(ClerkOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AppOrigins>()
            .Configure<IConfiguration>((origins, config) =>
                origins.Origins = config.GetSection(AppOrigins.Section).Get<string[]>() ?? [])
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ClerkOptions>, IOptions<AppOrigins>>((jwt, clerk, origins) =>
            {
                jwt.Authority = clerk.Value.Authority;
                jwt.MapInboundClaims = false;

                // Clerk's session tokens have no audience. The azp check below does that job.
                jwt.TokenValidationParameters.ValidateAudience = false;
                jwt.TokenValidationParameters.NameClaimType = "sub";

                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var party = context.Principal?.FindFirstValue(AuthorizedPartyClaim);
                        if (party is not null && !origins.Value.Origins.Contains(party, StringComparer.OrdinalIgnoreCase))
                            context.Fail($"The token was issued to {party}, which is not one of the app's origins.");

                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
