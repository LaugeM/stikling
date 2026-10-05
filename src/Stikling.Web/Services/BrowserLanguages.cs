using Microsoft.JSInterop;
using Stikling.Core.Names;

namespace Stikling.Web.Services;

/// <summary>The languages the browser is set to, which don't change while the app is open.</summary>
public sealed class BrowserLanguages(IJSRuntime js) : IDeviceLanguages
{
    private IReadOnlyList<string>? languages;

    public async Task<IReadOnlyList<string>> GetAsync()
    {
        if (languages is not null)
            return languages;

        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/page.js");
        return languages = await module.InvokeAsync<string[]>("languages");
    }
}
