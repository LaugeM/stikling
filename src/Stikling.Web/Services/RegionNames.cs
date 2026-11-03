using System.Globalization;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Services;

/// <summary>The names of Kew's level 3 areas in the language of the app.</summary>
public static class RegionNames
{
    /// <summary>The names for the area codes, sorted. A code with no name shows as the code.</summary>
    public static IReadOnlyList<string> Of(IEnumerable<string> codes) =>
    [
        .. codes
            .Select(code => Strings.Regions.ResourceManager.GetString(code, CultureInfo.CurrentUICulture) ?? code)
            .Order(StringComparer.Create(CultureInfo.CurrentCulture, true))
    ];
}
