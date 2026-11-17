namespace Stikling.Core.Labels;

/// <summary>
/// The link a QR code on a label holds, and the reading of a scanned one. The same link opens the plant
/// or cutting whichever of the two the id belongs to, so a label never has to know which it is for.
/// </summary>
public static class LabelLinks
{
    private const string AppHost = "stikling.app";

    /// <summary>
    /// The link a label's QR code holds, like HTTPS://STIKLING.APP/L/3F2504E04F8911D39A0C0305E82C3301.
    /// Scheme, host and id are in capitals because a QR code stores capitals, digits and a few marks in
    /// its compact alphanumeric mode, which gives a smaller code that scans from further away than the
    /// same link in lower case. Browsers read the scheme and host in any case, and the app matches
    /// the route in any case.
    /// </summary>
    public static string For(Guid id, string baseUri)
    {
        var uri = new Uri(baseUri, UriKind.Absolute);
        var path = uri.AbsolutePath.EndsWith('/') ? uri.AbsolutePath : uri.AbsolutePath + "/";
        return $"{uri.Scheme.ToUpperInvariant()}://{uri.Authority.ToUpperInvariant()}{path}L/{id.ToString("N").ToUpperInvariant()}";
    }

    /// <summary>
    /// A path inside the app for what was scanned, like "l/3f25..." or "plants/3f25-...", or null when it
    /// isn't a Stikling link. Links on stikling.app and on the host of <paramref name="baseUri"/> are
    /// accepted, in any case. The older "plants/..." and "propagations/..." links are there for stickers
    /// that were written with a copied address.
    /// </summary>
    public static string? AppPath(string? scanned, string baseUri)
    {
        if (string.IsNullOrWhiteSpace(scanned)
            || !Uri.TryCreate(scanned.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !Uri.TryCreate(baseUri, UriKind.Absolute, out var app))
            return null;

        string prefix;
        if (string.Equals(uri.Host, AppHost, StringComparison.OrdinalIgnoreCase))
            prefix = "/";
        else if (string.Equals(uri.Host, app.Host, StringComparison.OrdinalIgnoreCase))
            prefix = app.AbsolutePath.EndsWith('/') ? app.AbsolutePath : app.AbsolutePath + "/";
        else
            return null;

        var path = uri.AbsolutePath;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = path[prefix.Length..].TrimEnd('/').Split('/');
        if (parts.Length != 2)
            return null;

        switch (parts[0].ToLowerInvariant())
        {
            case "l":
                return ParseCode(parts[1]) is { } code ? $"l/{code.ToString("N")}" : null;
            case "plants" or "propagations":
                return ParseCode(parts[1]) is { } id ? $"{parts[0].ToLowerInvariant()}/{id.ToString("D")}" : null;
            default:
                return null;
        }
    }

    /// <summary>The id in the last part of a label link, 32 hex digits or a guid with dashes, in any case. Or null.</summary>
    public static Guid? ParseCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        code = code.Trim();
        // Guid.TryParse also takes braces and parentheses, which no label has
        foreach (var format in new[] { "N", "D" })
            if (Guid.TryParseExact(code, format, out var id) && id != Guid.Empty)
                return id;
        return null;
    }
}
