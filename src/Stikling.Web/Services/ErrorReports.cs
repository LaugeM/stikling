using Microsoft.JSInterop;
using Stikling.Core.Settings;

namespace Stikling.Web.Services;

/// <summary>
/// Turns error reports on or off. The choice is saved with the person's settings, and the copy in
/// localStorage (see wwwroot/js/errors.js) is what both Sentry SDKs check when they are about to send,
/// since it has to be known before Blazor starts. Reports are on unless the person turned them off.
/// </summary>
public sealed class ErrorReports(IJSRuntime js, SettingsService settings)
{
    public async Task<bool> IsOffAsync() => !await js.InvokeAsync<bool>("stiklingErrors.allowed");

    public async Task SetOffAsync(bool off)
    {
        await settings.SetErrorReportsOffAsync(off);
        await js.InvokeVoidAsync("stiklingErrors.setAllowed", !off);
    }

    /// <summary>
    /// Brings the device copy in line with the saved settings, e.g. after a restore or when the settings
    /// came from another device. With nothing saved, the local copy stands.
    /// </summary>
    public async Task ApplySavedAsync()
    {
        if (await settings.GetSavedAsync() is not { } saved || saved.ErrorReportsOff == await IsOffAsync())
            return;

        await js.InvokeVoidAsync("stiklingErrors.setAllowed", !saved.ErrorReportsOff);
    }

    /// <summary>Tells the browser's reporter which version this is. Nothing is sent in a development build.</summary>
    public async Task SetReleaseAsync()
    {
        if (AppVersion.Commit is { } commit)
            await js.InvokeVoidAsync("stiklingErrors.setRelease", commit);
    }
}
