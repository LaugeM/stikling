using System.Globalization;

namespace Stikling.Core.Text;

/// <summary>
/// For text that is saved into a record, like a line in a plant's history. It is written in English
/// whatever language the app is in, so it reads the same on every device and old lines keep matching.
/// </summary>
public static class SavedText
{
    public static T InEnglish<T>(Func<T> make)
    {
        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            return make();
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }
    }
}
