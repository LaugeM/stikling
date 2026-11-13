using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Measurements;

/// <summary>
/// How a measurement reads: "34 cm tall", "7 leaves", "3 cm of roots", "Nodes 3". A length is stored in cm
/// with 1 decimal and shown in cm or inches as the person chose, and a count is plain.
/// </summary>
public static class MeasureText
{
    /// <summary>The built-in kinds, in the order they are listed everywhere.</summary>
    public static readonly IReadOnlyList<MeasureKind> BuiltIn = [MeasureKind.Height, MeasureKind.Width, MeasureKind.Leaves, MeasureKind.RootLength];

    public static string KindName(MeasureKind kind) => kind switch
    {
        MeasureKind.Height => Strings.Measures.KindHeight,
        MeasureKind.Width => Strings.Measures.KindWidth,
        MeasureKind.Leaves => Strings.Measures.KindLeaves,
        MeasureKind.RootLength => Strings.Measures.KindRootLength,
        _ => kind.ToString()
    };

    /// <summary>The unit of a length, "cm" or the word for inches. <paramref name="inCm"/> is for text that is saved, which is always in cm.</summary>
    public static string UnitName(bool inCm = false) => inCm ? "cm" : Lengths.UnitName;

    /// <summary>The number for an input box showing a length stored in cm, to 1 decimal.</summary>
    public static decimal ToInput(decimal cm) => Math.Round(Lengths.CmToInput(cm), 1, MidpointRounding.AwayFromZero);

    /// <summary>The cm to store for a length typed in an input box, to 1 decimal.</summary>
    public static decimal FromInput(decimal typed) => Math.Round(Lengths.CmFromInput(typed), 1, MidpointRounding.AwayFromZero);

    /// <summary>A length stored in cm with its unit, like "34 cm" or "13.4 in".</summary>
    public static string Length(decimal cm, CultureInfo? culture = null, bool inCm = false) =>
        Number(inCm ? Math.Round(cm, 1, MidpointRounding.AwayFromZero) : ToInput(cm), culture) + " " + UnitName(inCm);

    /// <summary>A value with its unit when it is a length.</summary>
    public static string Value(bool isLength, decimal value, CultureInfo? culture = null, bool inCm = false) =>
        isLength ? Length(value, culture, inCm) : Number(value, culture);

    /// <summary>The phrase for one measurement, like "34 cm tall".</summary>
    public static string Phrase(Measurement measurement, CultureInfo? culture = null, bool inCm = false) =>
        Phrase(measurement.Kind, measurement.OwnKindName, measurement.IsLength, measurement.Value, culture, inCm);

    public static string Phrase(MeasureKind? kind, string? ownName, bool isLength, decimal value, CultureInfo? culture = null, bool inCm = false)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var shown = Value(isLength, value, culture, inCm);
        string Res(string name) => Strings.Measures.ResourceManager.GetString(name, culture) ?? name;
        return kind switch
        {
            MeasureKind.Height => string.Format(culture, Res("PhraseHeight"), shown),
            MeasureKind.Width => string.Format(culture, Res("PhraseWidth"), shown),
            MeasureKind.Leaves => string.Format(culture, Res(value == 1 ? "PhraseLeaves_One" : "PhraseLeaves_Other"), shown),
            MeasureKind.RootLength => string.Format(culture, Res("PhraseRootLength"), shown),
            _ => string.Format(culture, Res("PhraseOwn"), ownName ?? "", shown)
        };
    }

    /// <summary>The measurements of one entry, built-in kinds first and then the ones added by hand by name. Ones with no kind this version knows are left out.</summary>
    public static IReadOnlyList<Measurement> InOrder(IEnumerable<Measurement> measurements) =>
        [.. measurements.Where(m => m.HasKind)
            .OrderBy(m => m.Kind is { } k ? (int)k : int.MaxValue)
            .ThenBy(m => m.OwnKindName ?? "", StringComparer.CurrentCultureIgnoreCase)];

    private static string Number(decimal value, CultureInfo? culture) =>
        value.ToString("0.#", culture ?? CultureInfo.CurrentUICulture);
}
