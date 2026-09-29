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
}
