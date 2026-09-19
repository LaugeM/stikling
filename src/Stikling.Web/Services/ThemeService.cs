using Microsoft.JSInterop;

namespace Stikling.Web.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// Reads and changes the light/dark theme. The actual work happens in wwwroot/js/theme.js,
/// which also runs before Blazor starts so the page never flashes the wrong theme.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
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
        await js.InvokeAsync<string>("stiklingTheme.setMode", mode.ToString().ToLowerInvariant());
        Changed?.Invoke();
    }
}
