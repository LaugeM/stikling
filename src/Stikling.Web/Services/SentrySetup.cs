using Microsoft.JSInterop;
using Sentry;
using Stikling.Core.ErrorReports;

namespace Stikling.Web.Services;

/// <summary>
/// The options for the .NET side of Sentry, which Program.cs passes on. Reports are anonymous, hold no
/// query strings, and go out only while the person hasn't turned them off.
/// </summary>
public static class SentrySetup
{
    /// <summary>
    /// Set by Program.cs once the host is built. Reading the device copy of the setting has to be
    /// synchronous, since Sentry asks right when it is about to send. Until it's set, nothing is sent.
    /// </summary>
    public static IJSInProcessRuntime? Runtime { get; set; }

    public static void Configure(SentryOptions options, string? dsn)
    {
        // An empty address turns the SDK off. Leaving it unset makes the SDK throw on start.
        options.Dsn = string.IsNullOrWhiteSpace(dsn) ? string.Empty : dsn;

        options.Release = AppVersion.Commit;
        options.Environment = "production";
        options.SendDefaultPii = false;
        options.AutoSessionTracking = false;
        options.SetBeforeBreadcrumb(StripBreadcrumb);
        options.SetBeforeSend(Scrub);
    }

    private static bool Allowed()
    {
        try
        {
            return Runtime?.Invoke<bool>("stiklingErrors.allowed") ?? false;
        }
        catch
        {
            return false;
        }
    }

    internal static SentryEvent? Scrub(SentryEvent e, SentryHint hint)
    {
        if (!Allowed())
            return null;

        e.User = new SentryUser();
        DropMessages(e);
        if (e.Request is { } request)
        {
            request.Url = ReportScrubbing.WithoutQueryOrFragment(request.Url);
            request.QueryString = null;
            request.Cookies = null;
        }

        return e;
    }

    internal static Breadcrumb? StripBreadcrumb(Breadcrumb breadcrumb, SentryHint hint)
    {
        if (breadcrumb.Data is not { Count: > 0 } data
            || !data.Any(pair => pair.Key is "url" or "to" or "from" && pair.Value != ReportScrubbing.WithoutQueryOrFragment(pair.Value)))
            return breadcrumb;

        var stripped = data.ToDictionary(
            pair => pair.Key,
            pair => pair.Key is "url" or "to" or "from" ? ReportScrubbing.WithoutQueryOrFragment(pair.Value) ?? "" : pair.Value);
        // Breadcrumbs are made as things happen, so a new one has the same time to the second
        return new Breadcrumb(breadcrumb.Message!, breadcrumb.Type!, stripped, breadcrumb.Category, breadcrumb.Level);
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
