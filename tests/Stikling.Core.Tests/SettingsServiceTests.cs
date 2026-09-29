using Stikling.Core.Models;
using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

public class SettingsServiceTests
{
    private readonly FakeSettingsRepository repository = new();
    private readonly SettingsService service;

    public SettingsServiceTests() => service = new SettingsService(repository);

    [Fact]
    public async Task Nothing_saved_gives_the_default_with_the_fixed_id()
    {
        var settings = await service.GetAsync();

        Assert.Equal(UserSettings.SettingsId, settings.Id);
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.False(settings.PhotoReminder);
        Assert.Null(await service.GetSavedAsync());
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task Changing_the_theme_saves_it()
    {
        await service.SetThemeAsync(ThemeMode.Dark);

        var saved = Assert.Single(repository.Settings.Values);
        Assert.Equal(UserSettings.SettingsId, saved.Id);
        Assert.Equal(ThemeMode.Dark, saved.Theme);
        Assert.Equal(ThemeMode.Dark, (await service.GetAsync()).Theme);
    }

    [Fact]
    public async Task Changing_the_reminder_saves_it_and_keeps_the_theme()
    {
        await service.SetThemeAsync(ThemeMode.Light);
        await service.SetPhotoReminderAsync(true);

        var saved = Assert.Single(repository.Settings.Values);
        Assert.True(saved.PhotoReminder);
        Assert.Equal(ThemeMode.Light, saved.Theme);
    }
}
