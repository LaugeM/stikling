namespace Stikling.Core.Models;

/// <summary>
/// The choices behind the plant-sitter sheet. There is one for the collection, under a fixed id,
/// so every device holds the same record and the newest one wins. Every field is optional, so a
/// missing record reads as the defaults. The Hide fields are named for turning something off, so
/// older records read as shown.
/// </summary>
public sealed class SitterSheet : Entity
{
    /// <summary>The id of the one sheet record. Fixed, so no device makes a copy of its own.</summary>
    public static readonly Guid SheetId = new("b3c8d1e6-7a42-4f09-8e15-2d6a9c4f7b30");

    public SitterSheet() => Id = SheetId;

    public SheetLayout Layout { get; set; } = SheetLayout.Labels;

    public VisitPattern Visits { get; set; } = VisitPattern.TwiceAWeek;

    /// <summary>How a plant is watered when it doesn't say.</summary>
    public WaterMethod DefaultMethod { get; set; } = WaterMethod.Bottom;

    /// <summary>Use <see cref="OwnWords"/> in place of the usual wording for the watering method.</summary>
    public bool UseOwnWords { get; set; }

    public string? OwnWords { get; set; }

    /// <summary>The home has a moisture meter for the sitter to use.</summary>
    public bool HasMeter { get; set; }

    public string? MeterWhere { get; set; }

    public string? GeneralNote { get; set; }

    public bool HidePhotos { get; set; }
    public bool HidePlaces { get; set; }
    public bool HideHowWatered { get; set; }
    public bool HidePace { get; set; }
    public bool HideNotes { get; set; }

    /// <summary>Plants and propagations left off the sheet, by id.</summary>
    public List<Guid> LeftOff { get; set; } = [];
}
