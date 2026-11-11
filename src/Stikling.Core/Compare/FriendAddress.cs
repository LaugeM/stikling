using System.Text.RegularExpressions;
using Stikling.Core.Sharing;

namespace Stikling.Core.Compare;

/// <summary>Finds the address of a public page in what someone pasted.</summary>
public static partial class FriendAddress
{
    [GeneratedRegex(@"^[A-Za-z0-9_-]{22}$")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^(?:https?://)?(?<host>share\.stikling\.app|localhost(?::\d+)?)(?<path>/.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex HostRegex();

    /// <summary>
    /// The address of a page, "@name" in lower case or its token, from a link like
    /// https://share.stikling.app/@name or a local /s/{token}, or from "@name" on its own. Null for anything
    /// else, like a pasted list. A bare token isn't taken, since a plain word could look like one.
    /// </summary>
    public static string? Of(string? text)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s) || s.Any(char.IsWhiteSpace))
            return null;

        var withHost = false;
        if (HostRegex().Match(s) is { Success: true } m)
        {
            s = m.Groups["path"].Value;
            withHost = true;
        }

        s = s.Split('?', '#')[0];
        if (s.StartsWith("/s/", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
            withHost = true;
        }

        var segment = s.TrimStart('/').Split('/')[0];
        if (segment.StartsWith('@'))
        {
            var name = PublicPageRules.NormalizeName(segment);
            return name is not null && PublicPageRules.CheckName(name) is null ? "@" + name : null;
        }

        return withHost && TokenRegex().IsMatch(segment) ? segment : null;
    }
}
