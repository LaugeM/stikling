namespace Stikling.Core.Labels;

/// <summary>
/// Where the sign-in page may send someone back to. Only a path inside the app, so a link can't use
/// the sign-in to send people to another site.
/// </summary>
public static class ReturnPath
{
    /// <summary>True for a relative app path like "l/3F25..." that starts with a letter or digit, with no "//", ":" or backslash.</summary>
    public static bool IsSafe(string? path) =>
        !string.IsNullOrEmpty(path)
        && char.IsAsciiLetterOrDigit(path[0])
        && !path.Contains("//")
        && !path.Contains(':')
        && !path.Contains('\\')
        && !path.Any(char.IsControl);
}
