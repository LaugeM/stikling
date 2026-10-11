namespace Stikling.Core.ErrorReports;

/// <summary>What is taken out of an error report before it is sent.</summary>
public static class ReportScrubbing
{
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
