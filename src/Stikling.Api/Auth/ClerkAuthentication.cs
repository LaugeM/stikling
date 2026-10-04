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
            .Configure<IConfiguration>((origins, config) => origins.Origins = AppOrigins.From(config))
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
                        CheckAuthorizedParty(context, origins.Value);
                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }

    /// <summary>Turns the token away if it was issued to a site that isn't one of the app's origins.</summary>
    public static void CheckAuthorizedParty(TokenValidatedContext context, AppOrigins origins)
    {
        var party = context.Principal?.FindFirstValue(AuthorizedPartyClaim);
        if (party is not null && !origins.Allows(party))
            context.Fail($"The token was issued to {party}, which is not one of the app's origins.");
    }
}
