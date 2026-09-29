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
/// </summary>
public class AppOrigins
{
    public const string Section = "AppOrigins";

    [MinLength(1)]
    public string[] Origins { get; set; } = [];
}
