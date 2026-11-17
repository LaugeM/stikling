namespace Stikling.Core.ErrorReports;

/// <summary>What is taken out of an error report before it is sent.</summary>
public static class ReportScrubbing
{
    /// <summary>
    /// A calendar link's address with the token replaced, since anyone with the token can read the
    /// calendar. Everything after a /calendar/ segment goes. Null stays null.
    /// </summary>
    public static string? WithoutCalendarToken(string? url)
    {
        const string Segment = "/calendar/";
        if (url is null)
            return null;

        var at = url.IndexOf(Segment, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? url : url[..(at + Segment.Length)] + "[token]";
    }

    /// <summary>
    /// The address without its query string and fragment, which is the only part that could hold
    /// something other than ids. Null stays null.
    /// </summary>
    public static string? WithoutQueryOrFragment(string? url)
    {
        if (url is null)
            return null;

        var end = url.AsSpan().IndexOfAny('?', '#');
        return end < 0 ? url : url[..end];
    }
}
