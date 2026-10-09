using System.Reflection;
using System.Text.RegularExpressions;

namespace Stikling.Web.Services;

/// <summary>
/// The version the deploy stamps on the web app: the publish date as yyyy.MM.dd, then "+" and the commit
/// (see deploy.yml). A build on a developer's machine has neither, so it counts as a development build.
/// </summary>
public static class AppVersion
{
    private static readonly Regex Stamped = new(@"^(\d{4}\.\d{2}\.\d{2})(?:\+([0-9a-f]{7,40}))?$", RegexOptions.IgnoreCase);

    private static readonly Match Parsed = Stamped.Match(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "");

    /// <summary>The publish date, like "2026.10.09", or null in a development build.</summary>
    public static string? Date => Parsed.Success ? Parsed.Groups[1].Value : null;

    /// <summary>The full commit the build was made from, or null when it isn't known.</summary>
    public static string? Commit => Parsed.Success && Parsed.Groups[2].Success ? Parsed.Groups[2].Value : null;

    /// <summary>The first 7 characters of the commit.</summary>
    public static string? ShortCommit => Commit is { } commit ? commit[..7] : null;

    /// <summary>"Version 2026.10.09", or "Development build".</summary>
    public static string Label => Date is { } date ? $"Version {date}" : "Development build";
}
