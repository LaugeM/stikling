using Sentry;
using Sentry.Extensibility;
using Sentry.AspNetCore;
using Stikling.Api.People;
using Stikling.Core.ErrorReports;

namespace Stikling.Api.Reports;

/// <summary>
/// Error reports from the API to Sentry. With no address in the configuration (Sentry:Dsn) nothing is
/// sent, which is how it runs locally and in the tests. Reports are anonymous and hold no request
/// contents. The app's switch for error reports doesn't reach here, since these are about the server.
/// </summary>
public static class ErrorReporting
{
    public static void Configure(SentryAspNetCoreOptions options, string? dsn)
    {
        // An empty address turns the SDK off. Leaving it unset makes the SDK throw on start.
        options.Dsn = string.IsNullOrWhiteSpace(dsn) ? string.Empty : dsn;

        options.Environment = "production";
        options.SendDefaultPii = false;
        options.MaxRequestBodySize = RequestSize.None;
        options.AutoSessionTracking = false;

        // A deleted account is answered with 410, and a request that is cancelled is a client going away
        options.AddExceptionFilterForType<AccountDeletedException>();
        options.AddExceptionFilterForType<OperationCanceledException>();

        options.SetBeforeSend(Scrub);
    }

    /// <summary>Takes out who the request was from and anything the caller sent along.</summary>
    public static SentryEvent? Scrub(SentryEvent e, SentryHint hint)
    {
        e.User = new SentryUser();
        DropMessages(e);
        if (e.Request is { } request)
        {
            request.Url = ReportScrubbing.WithoutQueryOrFragment(request.Url);
            request.QueryString = null;
            request.Cookies = null;
            request.Headers.Clear();
            request.Env.Remove("REMOTE_ADDR");
        }

        return e;
    }

    /// <summary>
    /// An exception's message can quote what someone typed, like the name of a room, so only its type
    /// and where it happened are sent. A log line keeps its own fixed text, without the values put into it.
    /// </summary>
    internal static void DropMessages(SentryEvent e)
    {
        foreach (var exception in e.SentryExceptions ?? [])
            exception.Value = null;
        e.Message = e.Message?.Message is { } text ? new SentryMessage { Message = text } : null;
    }
}
