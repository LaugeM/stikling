using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.Reports;

namespace Stikling.Api.Notifications;

/// <summary>
/// <c>dotnet Stikling.Api.dll send-notifications</c>: the Container Apps job that sends the daily
/// notifications. It is a plain host with the database, error reports and the sender, and no web
/// server. It runs once and exits, with 0, or 1 on an error nobody planned for. It never applies
/// migrations, since the API does that when it starts.
/// </summary>
public static class NotificationJob
{
    public const string Command = "send-notifications";

    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddSentry(options =>
        {
            var dsn = builder.Configuration["Sentry:Dsn"];
            options.Dsn = string.IsNullOrWhiteSpace(dsn) ? string.Empty : dsn;
            options.Environment = "production";
            options.SendDefaultPii = false;
            options.AutoSessionTracking = false;
            options.SetBeforeSend(ErrorReporting.Scrub);
        });

        builder.Services.AddDbContext<StiklingDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("Stikling")
                ?? throw new InvalidOperationException("ConnectionStrings:Stikling is not set.")));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.Section));
        builder.Services.AddSingleton<IPushSender, WebPushSender>();
        builder.Services.AddScoped<NotificationSender>();

        using var host = builder.Build();
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<NotificationSender>().RunAsync(CancellationToken.None);
            return 0;
        }
        catch (Exception e)
        {
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(NotificationJob))
                .LogError(e, "The notification job failed.");
            return 1;
        }
    }
}
