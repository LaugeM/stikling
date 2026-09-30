using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>
/// Where signing in goes: the Clerk instance and the sync API. Both come from
/// wwwroot/appsettings.{Environment}.json, and signing in is only offered when all of it is set.
/// </summary>
public sealed record AccountSettings(string? ClerkPublishableKey, string? ClerkFrontendApi, Uri? ApiAddress)
{
    public bool IsComplete => ClerkPublishableKey is not null && ClerkFrontendApi is not null && ApiAddress is not null;

    public static AccountSettings From(IConfiguration config)
    {
        var api = config["Api:Address"];
        return new(
            config["Clerk:PublishableKey"],
            config["Clerk:FrontendApi"],
            // A trailing slash, so paths like "me" are added to it rather than replacing its last part
            string.IsNullOrEmpty(api) ? null : new Uri(api.TrimEnd('/') + "/"));
    }
}

/// <summary>Who is signed in on this device, as Clerk reports it.</summary>
public sealed record AccountState(bool SignedIn, string? Email);

/// <summary>Clerk's scripts couldn't be loaded, usually because there is no connection.</summary>
public sealed class AccountUnavailableException(Exception inner)
    : Exception("The sign-in service couldn't be reached.", inner);

/// <summary>
/// Signing in and out. Clerk has no Blazor library, so it is behind wwwroot/js/account.js, and this
/// is the only class that calls that file. Clerk's scripts are loaded on the first call that needs
/// them, never when the app starts.
/// </summary>
public sealed class AccountService(IJSRuntime js, NavigationManager nav, DeviceFiles files, AccountSettings settings)
    : IAsyncDisposable
{
    internal const string ModulePath = "./js/account.js";
    internal const string SignedInKey = "signed-in";

    private Task<IJSObjectReference>? module;
    private DotNetObjectReference<AccountService>? self;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>False when this build of the app has nowhere to sign in to.</summary>
    public bool IsAvailable => settings.IsComplete;

    /// <summary>Raised when someone signs in or out, on this page or in another tab.</summary>
    public event Action<AccountState>? Changed;

    /// <summary>
    /// Whether someone was signed in the last time this device checked. It is only a hint for
    /// whether to load Clerk at all, so a device that never signed in doesn't load it.
    /// </summary>
    public async Task<bool> WasSignedInAsync() => await files.GetAsync(SignedInKey) is not null;

    /// <summary>Loads Clerk if it isn't loaded yet, and says who is signed in.</summary>
    /// <exception cref="AccountUnavailableException">Clerk couldn't be loaded.</exception>
    public async Task<AccountState> LoadAsync()
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Signing in isn't set up in this build.");

        self ??= DotNetObjectReference.Create(this);
        AccountState state;
        try
        {
            state = await (await Module).InvokeAsync<AccountState>(
                "load", settings.ClerkFrontendApi, settings.ClerkPublishableKey, self);
        }
        catch (JSException e)
        {
            throw new AccountUnavailableException(e);
        }

        await RememberAsync(state);
        return state;
    }

    /// <summary>Shows Clerk's sign-in in the element. It goes to <paramref name="redirectUrl"/> once done.</summary>
    public async Task MountSignInAsync(ElementReference element, string redirectUrl) =>
        await (await Module).InvokeVoidAsync("mountSignIn", element, redirectUrl);

    public async Task UnmountSignInAsync(ElementReference element) =>
        await (await Module).InvokeVoidAsync("unmountSignIn", element);

    /// <summary>Signs out and stays on <paramref name="redirectUrl"/>. The data on the device is left as it is.</summary>
    public async Task SignOutAsync(string redirectUrl)
    {
        var state = await (await Module).InvokeAsync<AccountState>("signOut", redirectUrl);
        await RememberAsync(state);
    }

    /// <summary>
    /// Deletes the account at Clerk, which signs out too. The API's side of the account has to be
    /// deleted first, since the API can't be reached once the sign-in is gone.
    /// </summary>
    public async Task DeleteUserAsync()
    {
        var state = await (await Module).InvokeAsync<AccountState>("deleteUser");
        await RememberAsync(state);
    }

    /// <summary>What Clerk has about the signed-in person, for the download of their data. Null when nobody is signed in.</summary>
    public async Task<JsonElement?> GetProfileAsync() =>
        await (await Module).InvokeAsync<JsonElement?>("profile");

    /// <summary>A short-lived token that proves to the API who is signed in, or null when nobody is.</summary>
    public async Task<string?> GetTokenAsync() =>
        await (await Module).InvokeAsync<string?>("getToken");

    [JSInvokable]
    public async Task OnChanged(AccountState state)
    {
        await RememberAsync(state);
        Changed?.Invoke(state);
    }

    /// <summary>Clerk moves between pages through Blazor's router, so the app isn't reloaded.</summary>
    [JSInvokable]
    public void Navigate(string url, bool replace) => nav.NavigateTo(url, replace: replace);

    private Task RememberAsync(AccountState state) =>
        files.SetAsync(SignedInKey, state.SignedIn ? "yes" : null);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
        {
            await module.Result.InvokeVoidAsync("forget");
            await module.Result.DisposeAsync();
        }

        self?.Dispose();
    }
}
