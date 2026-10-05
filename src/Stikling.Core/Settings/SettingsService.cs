using Stikling.Core.Models;

namespace Stikling.Core.Settings;

public interface ISettingsRepository
{
    Task<UserSettings?> GetAsync(Guid id);

    Task SaveAsync(UserSettings settings);
}

/// <summary>The person's settings, kept as one record.</summary>
public sealed class SettingsService(ISettingsRepository settings)
{
    /// <summary>The saved settings, or an unsaved default when nothing has been changed yet.</summary>
    public async Task<UserSettings> GetAsync() =>
        await GetSavedAsync() ?? new UserSettings();

    /// <summary>The saved settings, or null when none have been saved on this device yet.</summary>
    public Task<UserSettings?> GetSavedAsync() => settings.GetAsync(UserSettings.SettingsId);

    public async Task SetThemeAsync(ThemeMode theme)
    {
        var current = await GetAsync();
        current.Theme = theme;
        await settings.SaveAsync(current);
    }

    public async Task SetPhotoReminderAsync(bool on)
    {
        var current = await GetAsync();
        current.PhotoReminder = on;
        await settings.SaveAsync(current);
    }

    public async Task SetEverydayNamesAsync(EverydayNameLanguage language)
    {
        var current = await GetAsync();
        current.EverydayNames = language;
        await settings.SaveAsync(current);
    }
}
