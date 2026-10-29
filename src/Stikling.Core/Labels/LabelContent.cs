using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Labels;

/// <summary>Which of the four things a label can show are switched on.</summary>
public sealed record LabelParts(bool Number = true, bool Name = true, bool Botanical = true, bool Date = false);

/// <summary>
/// What goes on the label of one plant or cutting, before the tick boxes and the size decide how much
/// of it fits. Worked out from the record and never saved.
/// </summary>
/// <param name="Number">The collection number, like 117 or 117a, or null.</param>
/// <param name="Name">The name the app shows for it.</param>
/// <param name="Botanical">The botanical name, only when it says something the name doesn't.</param>
/// <param name="DateLine">"Got 3 Mar 2025" or "Started 3 Oct 2026", or null when there is no date.</param>
public sealed record LabelContent(string? Number, string Name, string? Botanical, string? DateLine)
{
    public static LabelContent For(Plant plant) => new(
        CollectionNumbers.Normalize(plant.CollectionNumber),
        plant.DisplayName,
        BotanicalIfDifferent(plant.BotanicalName, plant.DisplayName),
        plant.AcquiredOn is { } acquired ? string.Format(CultureInfo.CurrentCulture, Strings.Shared.LabelGot, Got(acquired)) : null);

    public static LabelContent For(Propagation propagation) => new(
        CollectionNumbers.Normalize(propagation.CollectionNumber),
        propagation.DisplayName,
        BotanicalIfDifferent(propagation.BotanicalName, propagation.DisplayName),
        propagation.StartedOn == default
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.Shared.LabelStarted, propagation.StartedOn.ToString(Strings.Shared.LabelDayFormat, TextCulture.ForDates)));

    private static string? BotanicalIfDifferent(string? botanical, string name) =>
        string.IsNullOrWhiteSpace(botanical) || string.Equals(botanical.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : botanical.Trim();

    // The precision of the date decides how much of it is said: "3 Mar 2025", "Mar 2025" or "2025"
    private static string Got(LooseDate date) => date.Precision switch
    {
        DatePrecision.Day => date.Start.ToString(Strings.Shared.LabelDayFormat, TextCulture.ForDates),
        DatePrecision.Month => date.Start.ToString(Strings.Shared.LabelMonthFormat, TextCulture.ForDates),
        _ => date.Year.ToString("D4", CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// The file name of each label's picture: "stikling-label-117.png", or the name when there is no number.
    /// Two labels never get the same name, since the second would replace the first in a download folder.
    /// </summary>
    public static IReadOnlyList<string> FileNames(IReadOnlyList<LabelContent> labels)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>(labels.Count);
        foreach (var label in labels)
        {
            var stem = Slug(label.Number);
            if (stem.Length == 0)
                stem = Slug(label.Name);
            stem = stem.Length == 0 ? "stikling-label" : $"stikling-label-{stem}";

            var name = stem;
            for (var n = 2; !used.Add(name); n++)
                name = $"{stem}-{n}";
            names.Add($"{name}.png");
        }
        return names;
    }

    private static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var slug = new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        slug = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }
}
