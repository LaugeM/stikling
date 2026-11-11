using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>Small things pages need from the browser: scrolling, focus and the like (wwwroot/js/page.js).</summary>
public sealed class PageScripts(IJSRuntime js) : JsModule(js, "./js/page.js")
{
    /// <summary>Scrolls to the section the address names, since Blazor scrolls to the top after moving to a page.</summary>
    public async Task ScrollToFragmentAsync() =>
        await (await Module).InvokeVoidAsync("scrollToFragment");

    /// <summary>Brings an alert into view and moves focus to it.</summary>
    public async Task ShowAlertAsync(string id) =>
        await (await Module).InvokeVoidAsync("showAlert", id);

    /// <summary>Brings a field into view and focuses it.</summary>
    public async Task FocusFieldAsync(string id) =>
        await (await Module).InvokeVoidAsync("focusField", id);

    /// <summary>Brings a section that was just opened on the page into view.</summary>
    public async Task ScrollToIdAsync(string id) =>
        await (await Module).InvokeVoidAsync("scrollToId", id);

    /// <summary>Scrolls to a section of Settings on a phone, where it is one long page.</summary>
    public async Task ScrollToSectionOnPhoneAsync(string id) =>
        await (await Module).InvokeVoidAsync("scrollToSectionOnPhone", id);

    /// <summary>The languages the browser is set to, most preferred first, like ["da-DK", "da", "en"].</summary>
    public async Task<string[]> LanguagesAsync() =>
        await (await Module).InvokeAsync<string[]>("languages");

    /// <summary>Makes Enter in a search box pick its first result. Dispose what it returns when the box goes away.</summary>
    public async Task<IJSObjectReference> EnterPicksFirstAsync(ElementReference input) =>
        await (await Module).InvokeAsync<IJSObjectReference>("enterPicksFirst", input);

    /// <summary>Scrolls to an element and fills it for a moment, for a result opened from Search.</summary>
    public async Task ShowFoundAsync(string id) =>
        await (await Module).InvokeVoidAsync("showFound", id);
}
