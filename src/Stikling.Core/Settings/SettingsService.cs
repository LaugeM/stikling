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

    public async Task SetGooglePhotosAsync(bool on)
    {
        var current = await GetAsync();
        current.GooglePhotos = on;
        await settings.SaveAsync(current);
    }

    public async Task SetErrorReportsOffAsync(bool off)
    {
        var current = await GetAsync();
        current.ErrorReportsOff = off;
        await settings.SaveAsync(current);
    }

    public async Task SetDailyRoundAsync(bool on)
    {
        var current = await GetAsync();
        current.DailyRound = on;
        await settings.SaveAsync(current);
    }

    public async Task SetRoundDaysAsync(IEnumerable<Weekday> days)
    {
        var current = await GetAsync();
        var chosen = days.Distinct().Order().ToList();
        current.RoundDays = chosen.Count == 0 ? null : chosen;
        await settings.SaveAsync(current);
    }

    public async Task SetWateringReminderAsync(bool on)
    {
        var current = await GetAsync();
        current.WateringReminder = on;
        await settings.SaveAsync(current);
    }

    public async Task SetRecentChangesDaysAsync(int days)
    {
        var current = await GetAsync();
        current.RecentChangesDays = Math.Max(0, days);
        await settings.SaveAsync(current);
    }

    /// <summary>Sets how many rows the list on the day back shows: 3, 5, 10, or 0 for all of them.</summary>
    public async Task SetBackListLengthAsync(int length)
    {
        if (length is not (0 or 3 or 5 or 10))
            throw new ArgumentOutOfRangeException(nameof(length), "The list shows 3, 5, 10 or all of them.");

        var current = await GetAsync();
        current.BackListLength = length;
        await settings.SaveAsync(current);
    }

    /// <summary>Sets how the Water plants screen asks about each plant.</summary>
    public async Task SetWaterCheckAsync(WaterCheck check)
    {
        var current = await GetAsync();
        current.WaterCheck = check;
        await settings.SaveAsync(current);
    }

    public async Task SetEverydayNamesAsync(EverydayNameLanguage language)
    {
        var current = await GetAsync();
        current.EverydayNames = language;
        await settings.SaveAsync(current);
    }

    /// <summary>Hides the year section on Today for the rest of that year's season.</summary>
    public async Task HideYearAsync(int year)
    {
        var current = await GetAsync();
        current.YearHiddenFor = year;
        await settings.SaveAsync(current);
    }
}
