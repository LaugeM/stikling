using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Stikling.Api.Auth;

/// <summary>The <c>RequestLimits</c> section of the configuration.</summary>
public sealed class RequestLimitOptions
{
    public const string Section = "RequestLimits";

    /// <summary>How many requests a signed-in person can send at once before they are slowed down.</summary>
    public int Burst { get; set; } = 1000;

    /// <summary>How many more requests they get each minute after that.</summary>
    public int PerMinute { get; set; } = 600;
}

/// <summary>
/// Keeps one account from sending so much that the server slows down for everyone, or runs up the
/// bill until the cost stop takes it down. Each signed-in person has a bucket of requests that
/// refills each second. A first sync with a lot of photos can go past the burst, and the app then
/// waits and carries on. Requests without a sign-in aren't counted: they are turned away before
/// they reach the database, and the health check has to answer whatever happens.
/// </summary>
public static class RequestLimits
{
    public static IServiceCollection AddRequestLimits(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RequestLimitOptions>(config.GetSection(RequestLimitOptions.Section));
        services.AddRateLimiter(limiter =>
        {
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // The Clerk user id, or the test person's in Development
                if (context.User.Identity is not { IsAuthenticated: true, Name: { } person })
                    return RateLimitPartition.GetNoLimiter("");

                var options = context.RequestServices.GetRequiredService<IOptions<RequestLimitOptions>>().Value;
                return RateLimitPartition.GetTokenBucketLimiter(person, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.Burst,
                    TokensPerPeriod = Math.Max(1, options.PerMinute / 60),
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueLimit = 0,
                });
            });

            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (rejected, _) =>
            {
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait))
                    rejected.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
        });
        return services;
    }
}
