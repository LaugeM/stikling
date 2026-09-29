using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Settings;

namespace Stikling.Web.Services;

/// <summary>
/// Reads and changes the light/dark theme. The actual work happens in wwwroot/js/theme.js,
/// which also runs before Blazor starts so the page never flashes the wrong theme. The choice is
/// saved with the person's settings, and the copy in localStorage is only for that first paint.
/// </summary>
public sealed class ThemeService(IJSRuntime js, SettingsService settings)
{
    /// <summary>Raised after the mode is changed, so every theme control can update itself.</summary>
    public event Action? Changed;

    public async Task<ThemeMode> GetModeAsync()
    {
        var mode = await js.InvokeAsync<string>("stiklingTheme.getMode");
        return Enum.TryParse<ThemeMode>(mode, ignoreCase: true, out var parsed) ? parsed : ThemeMode.System;
    }

    /// <summary>True when the dark theme is showing right now (including "System" on a dark OS).</summary>
    public async Task<bool> IsDarkAsync() => await js.InvokeAsync<string>("stiklingTheme.current") == "dark";

    public async Task SetModeAsync(ThemeMode mode)
    {
        await settings.SetThemeAsync(mode);
        await js.InvokeAsync<string>("stiklingTheme.setMode", mode.ToString().ToLowerInvariant());
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies the saved theme when it differs from the one showing, e.g. after a restore or when
    /// the settings came from another device. With nothing saved, the local copy stands.
    /// </summary>
    public async Task ApplySavedAsync()
    {
        if (await settings.GetSavedAsync() is not { } saved || saved.Theme == await GetModeAsync())
            return;

        await js.InvokeAsync<string>("stiklingTheme.setMode", saved.Theme.ToString().ToLowerInvariant());
        Changed?.Invoke();
    }
}
