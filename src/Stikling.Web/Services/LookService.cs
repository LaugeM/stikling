using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Settings;

namespace Stikling.Web.Services;

/// <summary>
/// The text size, contrast, motion and large buttons choices under Look in Settings. They are saved with
/// the person's settings so every device follows. The work happens in wwwroot/js/theme.js, which also runs
/// before Blazor starts from a copy in localStorage, so the first paint already has the right look.
/// Contrast and motion off means follow the phone, and theme.js does that part.
/// </summary>
public sealed class LookService(IJSRuntime js, SettingsService settings)
{
    public async Task<TextSize> GetTextSizeAsync() => (await settings.GetAsync()).TextSize ?? TextSize.Normal;

    public async Task SetTextSizeAsync(TextSize size)
    {
        await settings.SetTextSizeAsync(size);
        await ApplyAsync(await settings.GetAsync());
    }

    public async Task SetHighContrastAsync(bool on)
    {
        await settings.SetHighContrastAsync(on);
        await ApplyAsync(await settings.GetAsync());
    }

    public async Task SetReduceMotionAsync(bool on)
    {
        await settings.SetReduceMotionAsync(on);
        await ApplyAsync(await settings.GetAsync());
    }

    public async Task SetLargeButtonsAsync(bool on)
    {
        await settings.SetLargeButtonsAsync(on);
        await ApplyAsync(await settings.GetAsync());
    }

    /// <summary>
    /// Applies the saved look, e.g. after a restore or when the settings came from another device. With
    /// nothing saved, the copy on this device stands.
    /// </summary>
    public async Task ApplySavedAsync()
    {
        if (await settings.GetSavedAsync() is { } saved)
            await ApplyAsync(saved);
    }

    private async Task ApplyAsync(UserSettings saved) =>
        await js.InvokeVoidAsync("stiklingTheme.setLook", new
        {
            text = (saved.TextSize ?? TextSize.Normal).ToString().ToLowerInvariant(),
            contrast = saved.HighContrast,
            motion = saved.ReduceMotion,
            large = saved.LargeButtons
        });
}
