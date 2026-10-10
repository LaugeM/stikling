namespace Stikling.Api.Sharing;

/// <summary>The <c>Share</c> section of the configuration.</summary>
public sealed class ShareOptions
{
    public const string Section = "Share";

    /// <summary>Where a link's page is shown, without the token: https://share.stikling.app hosted, http://localhost:5180/s locally.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The host that serves the pages from its root, like share.stikling.app. Unset locally, where /s/... is used.</summary>
    public string? Host { get; set; }

    /// <summary>The address of a page, with the token.</summary>
    public string UrlOf(string token) => $"{BaseUrl.TrimEnd('/')}/{token}";

    /// <summary>True when the request came in on the share host.</summary>
    public bool IsShareHost(string? host) =>
        !string.IsNullOrWhiteSpace(Host) && string.Equals(host, Host, StringComparison.OrdinalIgnoreCase);
}
