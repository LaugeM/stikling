using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Labels;

/// <summary>The three ways to get labels out of the app.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LabelTab
{
    /// <summary>Pictures to share with the app of a phone label maker.</summary>
    App,
    /// <summary>A page per label, for a tape printer installed on a computer.</summary>
    Tape,
    /// <summary>A sheet of tags on a normal printer.</summary>
    Sheet
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LabelPaper { A4, Letter }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LabelSheetLayout { Pots, Stakes }

/// <summary>A size in the Label maker app tab. The width is the way the text runs.</summary>
public sealed record LabelAppSize(string Id, string Label, double WidthMm, double HeightMm);

/// <summary>
/// How big a label is and what is drawn around it. The width is the way the text runs, so a tape label is
/// as wide as it is long. A stake is the one tall shape, with its text turned to run down it.
/// </summary>
/// <param name="Cut">"none", "box" for the cut lines of a tag, or "point" for a stake with a pointed end.</param>
public sealed record LabelShape(double WidthMm, double HeightMm, string Cut = "none")
{
    /// <summary>Long and narrow, like a plant stake: the number goes at the top and the name runs down.</summary>
    public bool Stake => HeightMm >= 2.5 * WidthMm;

    /// <summary>Too low for more than one line: the number, then the name.</summary>
    public bool OneLine => !Stake && HeightMm <= 12;
}

/// <summary>
/// What someone chose on the Labels page, kept on this device and never in a record. Anything unknown or out
/// of range in what was saved comes back as the default, so an older or damaged copy still opens the page.
/// </summary>
public sealed class LabelSettings
{
    public const string Key = "labels-settings";

    public static readonly IReadOnlyList<LabelAppSize> AppSizes =
    [
        new("12x40", "12 × 40 mm", 40, 12),
        new("14x30", "14 × 30 mm", 30, 14),
        new("40x30", "40 × 30 mm", 40, 30),
        new("50x30", "50 × 30 mm", 50, 30),
    ];

    public const string Custom = "custom";
    public const int CustomMin = 10;
    public const int CustomMax = 100;

    public static readonly IReadOnlyList<int> TapeWidths = [9, 12, 18, 24];
    public static readonly IReadOnlyList<int> TapeLengths = [30, 40, 50, 60];

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LabelTab Tab { get; set; } = LabelTab.App;
    public string AppSize { get; set; } = "12x40";
    public int CustomWidthMm { get; set; } = 40;
    public int CustomLengthMm { get; set; } = 30;
    public int TapeMm { get; set; } = 12;
    public int TapeLengthMm { get; set; } = 40;
    public LabelPaper Paper { get; set; } = LabelPaper.A4;
    public LabelSheetLayout Layout { get; set; } = LabelSheetLayout.Pots;
    public LabelParts Parts { get; set; } = new();

    /// <summary>The settings from what was saved, or the defaults when there is none or it can't be read.</summary>
    public static LabelSettings FromJson(string? json)
    {
        LabelSettings? settings = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                settings = JsonSerializer.Deserialize<LabelSettings>(json, Json);
            }
            catch (JsonException)
            {
            }
        }
        return (settings ?? new LabelSettings()).Normalized();
    }

    public string ToJson() => JsonSerializer.Serialize(Normalized(), Json);

    /// <summary>The same settings with every value brought back into what the page offers.</summary>
    public LabelSettings Normalized() => new()
    {
        Tab = Enum.IsDefined(Tab) ? Tab : LabelTab.App,
        AppSize = AppSize == Custom || AppSizes.Any(s => s.Id == AppSize) ? AppSize : "12x40",
        CustomWidthMm = Math.Clamp(CustomWidthMm, CustomMin, CustomMax),
        CustomLengthMm = Math.Clamp(CustomLengthMm, CustomMin, CustomMax),
        TapeMm = TapeWidths.Contains(TapeMm) ? TapeMm : 12,
        TapeLengthMm = TapeLengths.Contains(TapeLengthMm) ? TapeLengthMm : 40,
        Paper = Enum.IsDefined(Paper) ? Paper : LabelPaper.A4,
        Layout = Enum.IsDefined(Layout) ? Layout : LabelSheetLayout.Pots,
        Parts = Parts ?? new LabelParts(),
    };

    /// <summary>The size of one label for the tab that is open.</summary>
    public LabelShape Shape()
    {
        var settings = Normalized();
        switch (settings.Tab)
        {
            case LabelTab.Tape:
                return new LabelShape(settings.TapeLengthMm, settings.TapeMm);
            case LabelTab.Sheet:
                return settings.Layout == LabelSheetLayout.Stakes
                    ? new LabelShape(15, 120, "point")
                    : new LabelShape(50, 25, "box");
            default:
                if (settings.AppSize == Custom)
                    return new LabelShape(settings.CustomWidthMm, settings.CustomLengthMm);
                var size = AppSizes.First(s => s.Id == settings.AppSize);
                return new LabelShape(size.WidthMm, size.HeightMm);
        }
    }

    /// <summary>
    /// The page rule that makes the print dialog's page the right size, or null for the tab that doesn't print.
    /// A tape label is a page of its own, and a sheet is a paper the tags flow across.
    /// </summary>
    public string? PageRule()
    {
        var settings = Normalized();
        return settings.Tab switch
        {
            LabelTab.Tape => string.Create(CultureInfo.InvariantCulture, $"@page {{ size: {settings.TapeLengthMm}mm {settings.TapeMm}mm; margin: 0 }}"),
            LabelTab.Sheet => $"@page {{ size: {(settings.Paper == LabelPaper.A4 ? "A4" : "letter")}; margin: 10mm }}",
            _ => null
        };
    }

    /// <summary>
    /// Why the botanical name and the date can't be ticked, or null when there is room. Said under the tick boxes.
    /// </summary>
    public string? NoRoomNote()
    {
        var settings = Normalized();
        if (!settings.Shape().OneLine)
            return null;
        return settings.Tab == LabelTab.Tape
            ? $"No room on {settings.TapeMm} mm tape."
            : "No room on a label this low.";
    }
}
