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

    /// <summary>
    /// The year whose section on Today was hidden for its season ("Hide for this season"), so it
    /// stays hidden on every device. Null when none was. Older settings without it read as null.
    /// </summary>
    public int? YearHiddenFor { get; set; }

    /// <summary>
    /// How many rows "Look at these first" shows on the day back. Null means 5, 0 means all of
    /// them, and 3 and 10 are the other choices. Older settings without it read as null.
    /// </summary>
    public int? BackListLength { get; set; }

    /// <summary>
    /// How the Water plants screen asks about each plant: with the meter or by feeling the soil. Null
    /// until it's chosen there, and then it follows whether any meter reading has been logged.
    /// Older settings without it read as null.
    /// </summary>
    public WaterCheck? WaterCheck { get; set; }

    /// <summary>
    /// The weekdays the person goes round their plants. Checks come up on the round day before
    /// they're due. Null or empty means off. Older settings without it read as null.
    /// </summary>
    public List<Weekday>? RoundDays { get; set; }

    /// <summary>
    /// How many clean weeks a case being watched needs before it's ready to be resolved. Null
    /// means 4. Older settings without it read as null.
    /// </summary>
    public int? CleanWeeks { get; set; }

    /// <summary>
    /// How many days a plant is kept apart when it goes into quarantine. Null means
    /// <see cref="Plant.DefaultQuarantineDays"/>. Older settings without it read as null.
    /// </summary>
    public int? QuarantineDays { get; set; }

    /// <summary>
    /// How many cm wider across the top a new pot can be before Pot up says so. Null means 5.
    /// Older settings without it read as null.
    /// </summary>
    public decimal? PotUpWarnCm { get; set; }

    /// <summary>
    /// Whether a plant that is added as bought, swapped or a gift starts in quarantine. Off unless
    /// it's turned on. Older settings without it read as off.
    /// </summary>
    public bool QuarantineNewPlants { get; set; }

    /// <summary>
    /// The time of day the daily notification is sent, on a quarter hour. Null until notifications
    /// are turned on on a device, which sets it to 08:00. Older settings without it read as null.
    /// </summary>
    public TimeOnly? NotifyAt { get; set; }

    /// <summary>
    /// Which days the notification is sent on. Older settings without it read as every day.
    /// </summary>
    public NotifyDays NotifyOn { get; set; } = NotifyDays.EveryDay;

    /// <summary>
    /// The weekdays picked when <see cref="NotifyOn"/> is <see cref="NotifyDays.Picked"/>. With none
    /// picked nothing is sent. Null or empty means none. Older settings without it read as null.
    /// </summary>
    public List<Weekday>? NotifyWeekdays { get; set; }
}
