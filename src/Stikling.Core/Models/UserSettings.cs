namespace Stikling.Core.Models;

/// <summary>
/// The person's own settings, the ones they would expect on every device. There is only ever one,
/// under a fixed id, so two devices hold the same record and the newest one wins.
/// </summary>
public sealed class UserSettings : Entity
{
    /// <summary>The id of the one settings record. Fixed, so no device makes a copy of its own.</summary>
    public static readonly Guid SettingsId = new("5e7a1c40-3b2d-4f86-9a17-6d0c8e2b4f51");

    public UserSettings() => Id = SettingsId;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>The monthly photo reminder on Today. Off until turned on in Settings.</summary>
    public bool PhotoReminder { get; set; }

    /// <summary>Picking photos from Google Photos. Off until turned on in Settings.</summary>
    public bool GooglePhotos { get; set; }

    /// <summary>
    /// Error reports from the app. On unless turned off in Settings, so the field is named for turning
    /// them off and older settings without it read as on.
    /// </summary>
    public bool ErrorReportsOff { get; set; }

    /// <summary>The daily round on Today. Off until turned on in Settings.</summary>
    public bool DailyRound { get; set; }

    /// <summary>The watering reminder on Today. Off until turned on in Settings.</summary>
    public bool WateringReminder { get; set; }

    /// <summary>
    /// The language of the everyday names shown next to botanical names. Null until it's chosen in
    /// Settings, and then it's Danish on a device whose browser lists Danish and English elsewhere.
    /// </summary>
    public EverydayNameLanguage? EverydayNames { get; set; }

    /// <summary>
    /// How many days a plant's page keeps saying what happened to it lately, like a repot or a move.
    /// Null means the default of 14 days, and 0 means off. Older settings without it read as null.
    /// </summary>
    public int? RecentChangesDays { get; set; }
}
