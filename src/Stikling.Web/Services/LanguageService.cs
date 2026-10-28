using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Stikling.Core.Names;
using Stikling.Core.Settings;

namespace Stikling.Web.Services;

/// <summary>
/// The language the app is shown in. Program.cs picks it before the app starts, from the choice kept in
/// localStorage by wwwroot/js/language.js, or else from the browser. The choice itself is saved with the
/// person's settings, and the copy in localStorage is only there for that start.
/// </summary>
public sealed class LanguageService(
    IJSRuntime js,
    SettingsService settings,
    NavigationManager navigation,
    IWebAssemblyHostEnvironment environment) : IAppLanguage
{
    /// <summary>The code of the language in use, like "da". It is English for a culture the app has no language for.</summary>
    public string Current => Code;

    string IAppLanguage.Code => Code;

    private static string Code =>
        AppLanguages.All.FirstOrDefault(l => l.Code == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)?.Code
        ?? AppLanguages.English;

    /// <summary>The languages Settings lets someone pick. Languages still being translated only show in development builds.</summary>
    public IReadOnlyList<AppLanguage> Offered => AppLanguages.Offered(environment.IsDevelopment());

    /// <summary>
    /// Saves the choice, keeps a copy for the next start and restarts the app, since the text is
    /// chosen when it starts.
    /// </summary>
    public async Task SetAsync(string code)
    {
        if (!Offered.Any(l => l.Code == code))
            return;

        await settings.SetLanguageAsync(code);
        await js.InvokeVoidAsync("stiklingLanguage.set", code);
        navigation.Refresh(forceReload: true);
    }

    /// <summary>
    /// Writes the saved language to the copy used at start-up when it differs, e.g. after a restore or
    /// when the settings came from another device. It doesn't restart the app, so nothing changes under
    /// someone, and the language shows from the next start.
    /// </summary>
    public async Task ApplySavedAsync()
    {
        if (await settings.GetSavedAsync() is not { Language: { } saved } || !Offered.Any(l => l.Code == saved))
            return;

        var onDevice = await js.InvokeAsync<string?>("stiklingLanguage.get");
        if (onDevice != saved)
            await js.InvokeVoidAsync("stiklingLanguage.set", saved);
    }
}
