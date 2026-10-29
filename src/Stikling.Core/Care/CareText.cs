using System.Globalization;

namespace Stikling.Core.Care;

/// <summary>Fills the placeholders of a text from the resource files, in the current culture.</summary>
internal static class CareText
{
    public static string F(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);
}
