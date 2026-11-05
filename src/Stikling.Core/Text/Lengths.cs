using System.Globalization;
using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Text;

/// <summary>
/// Shows lengths in cm or inches and converts what is typed. Sizes are always stored in cm and mm, so
/// only what is shown and typed changes. The unit is set from the person's settings in the web project
/// only, so the server keeps centimetres.
/// </summary>
public static class Lengths
{
    private const decimal MmPerInch = 25.4m;
    private const decimal CmPerInch = 2.54m;

    public static LengthUnit Unit { get; set; } = LengthUnit.Centimetres;

    public static bool IsInches => Unit == LengthUnit.Inches;

    /// <summary>The unit of a field stored in cm: "cm" or the word for inches.</summary>
    public static string UnitName => IsInches ? Strings.Shared.UnitInch : "cm";

    /// <summary>The unit of a field stored in mm: "mm" or the word for inches.</summary>
    public static string SmallUnitName => IsInches ? Strings.Shared.UnitInch : "mm";

    // Cm and Mm are for showing, with the decimal sign of the language. Never save them: Pot.StoredName and Pot.Size are the invariant ones.

    /// <summary>A length stored in cm with its unit, like "13 cm" or "5.12 in".</summary>
    public static string Cm(decimal cm) =>
        Number(IsInches ? cm / CmPerInch : cm) + " " + UnitName;

    /// <summary>A length stored in mm with its unit, like "4 mm" or "0.16 in".</summary>
    public static string Mm(decimal mm) =>
        Number(IsInches ? mm / MmPerInch : mm) + " " + SmallUnitName;

    /// <summary>The number for an input box showing a length stored in cm.</summary>
    public static decimal CmToInput(decimal cm) => IsInches ? Math.Round(cm / CmPerInch, 2) : cm;

    /// <summary>The number for an input box showing a length stored in mm.</summary>
    public static decimal MmToInput(decimal mm) => IsInches ? Math.Round(mm / MmPerInch, 2) : mm;

    /// <summary>The cm to store for a number typed in an input box. Not rounded, so whole and half inches come back exact.</summary>
    public static decimal CmFromInput(decimal typed) => IsInches ? typed * CmPerInch : typed;

    /// <summary>The mm to store for a number typed in an input box.</summary>
    public static decimal MmFromInput(decimal typed) => IsInches ? typed * MmPerInch : typed;

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
