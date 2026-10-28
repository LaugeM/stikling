using System.Globalization;
using System.Text;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Wishlist;

/// <summary>What goes in the text of a shared wishlist. Notes and the price are left out unless asked for.</summary>
public sealed record WishlistTextOptions(bool Notes = false, bool MostToPay = false, bool AppLine = true);

/// <summary>The wishlist as plain text, to send in a message. Only the wishes still open are in it.</summary>
public static class WishlistText
{
    public static string AppLine => Strings.Shared.KeptInStikling;

    public static string Write(IEnumerable<Wish> wishes, EverydayNameBook? names, WishlistTextOptions options)
    {
        var open = wishes
            .Where(w => !w.IsDeleted && !w.IsGot)
            .OrderBy(w => w.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var text = new StringBuilder(Strings.Shared.WishlistTextHeading);
        foreach (var wish in open)
        {
            text.Append("\n- ").Append(Line(wish, names));
            if (options.Notes && wish.Notes?.Trim() is { Length: > 0 } notes)
                text.Append("\n  ").Append(notes.ReplaceLineEndings(" "));
            if (options.MostToPay && wish.MostToPay is { } price)
                text.Append("\n  ").Append(string.Format(CultureInfo.CurrentCulture, Strings.Shared.WishlistTextWouldPay, price.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        if (options.AppLine)
            text.Append("\n\n").Append(AppLine);
        return text.ToString();
    }

    // The botanical name with the everyday one in brackets, or the name typed in when there's no genus
    private static string Line(Wish wish, EverydayNameBook? names)
    {
        if (wish.BotanicalName is not { } botanical)
            return wish.DisplayName;

        var called = names?.For(wish.Genus, PlantNames.SpeciesToLookUp(wish.Species, wish.NameQualifier), wish.Cultivar);
        if (string.IsNullOrWhiteSpace(called))
            called = wish.Name?.Trim();
        return string.IsNullOrWhiteSpace(called) ? botanical : $"{botanical} ({called})";
    }
}
