using System.ComponentModel.DataAnnotations;

namespace Stikling.Api.Auth;

/// <summary>The <c>Clerk</c> section of the configuration.</summary>
public class ClerkOptions
{
    public const string Section = "Clerk";

    /// <summary>
    /// The instance's Frontend API URL. It is the issuer of the session tokens, and the API
    /// fetches Clerk's signing keys from it.
    /// </summary>
    [Required, Url]
    public string Authority { get; set; } = "";
}

/// <summary>
/// The <c>AppOrigins</c> list in the configuration: the origins the app is served from. Browsers
/// on those origins may call the API, and a session token has to have been issued to one of them.
/// An origin can end in <c>:*</c> for any port, like <c>http://localhost:*</c> in Development,
/// where the dev server runs on whichever port is free.
/// </summary>
public class AppOrigins
{
    public const string Section = "AppOrigins";

    [MinLength(1)]
    public string[] Origins { get; set; } = [];

    /// <summary>
    /// The list from the configuration. A trailing slash is dropped, since browsers send an
    /// origin without one and it would otherwise never match.
    /// </summary>
    public static string[] From(IConfiguration config) =>
        (config.GetSection(Section).Get<string[]>() ?? []).Select(origin => origin.TrimEnd('/')).ToArray();

    public bool Allows(string origin) => Origins.Any(allowed => Matches(allowed, origin));

    private static bool Matches(string allowed, string origin)
    {
        if (string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase))
            return true;

        // "http://localhost:*" takes any port, and only a port
        if (!allowed.EndsWith(":*", StringComparison.Ordinal))
            return false;

        var start = allowed[..^1];
        return origin.Length > start.Length
            && origin.StartsWith(start, StringComparison.OrdinalIgnoreCase)
            && origin[start.Length..].All(char.IsAsciiDigit);
    }
}
