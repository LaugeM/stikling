namespace Stikling.Api.Sharing;

/// <summary>The <c>Share</c> section of the configuration.</summary>
public sealed class ShareOptions
{
    public const string Section = "Share";

    /// <summary>Where a link's page is shown, without the token: https://share.stikling.app hosted, http://localhost:5180/s locally.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Where the app is, for the link on a page that opens a copy of it in the app: https://stikling.app/ hosted, http://localhost:5170/ locally.</summary>
    public string AppUrl { get; set; } = "https://stikling.app/";

    /// <summary>The host that serves the pages from its root, like share.stikling.app. Unset locally, where /s/... is used.</summary>
    public string? Host { get; set; }

    /// <summary>How long the public pages' sitemap is kept before it is built again. 0 builds it every time, as in the tests.</summary>
    public int SitemapCacheMinutes { get; set; } = 10;

    /// <summary>The address of a page, with the token.</summary>
    public string UrlOf(string token) => $"{BaseUrl.TrimEnd('/')}/{token}";

    /// <summary>The address of a public page by its name, like https://share.stikling.app/@name.</summary>
    public string NamedUrlOf(string name) => $"{BaseUrl.TrimEnd('/')}/@{name}";

    /// <summary>True when the request came in on the share host.</summary>
    public bool IsShareHost(string? host) =>
        !string.IsNullOrWhiteSpace(Host) && string.Equals(host, Host, StringComparison.OrdinalIgnoreCase);
}
