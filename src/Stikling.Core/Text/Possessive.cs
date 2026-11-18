using System.Globalization;

namespace Stikling.Core.Text;

/// <summary>
/// A name with the ending that says it is theirs, like "Alma's" or, in Danish, "Almas" and "Jens'".
/// The ending depends on how the name ends, which a resource string can't say.
/// </summary>
public static class Possessive
{
    public static string Of(string name) => Of(name, CultureInfo.CurrentUICulture);

    public static string Of(string name, CultureInfo culture)
    {
        name = name.Trim();
        if (name.Length == 0)
            return name;

        if (culture.TwoLetterISOLanguageName == "da")
            return char.ToLowerInvariant(name[^1]) is 's' or 'x' or 'z' ? name + "'" : name + "s";

        return name + "'s";
    }
}
