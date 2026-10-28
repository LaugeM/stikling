using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Tour;

namespace Stikling.Web.Services;

/// <summary>
/// Where the guided tour is. It lives in memory only: nothing is stored, and reloading the page ends the tour.
/// The steps and the moving between them are in <see cref="GuidedTour"/>. This adds the navigating, the mark on
/// the page and the Escape key, which need the browser.
/// </summary>
public sealed class TourService(NavigationManager navigation, IJSRuntime js) : IAsyncDisposable
{
    private readonly GuidedTour tour = new();
    private Task<IJSObjectReference>? module;
    private DotNetObjectReference<TourService>? self;

    /// <summary>Raised when the tour starts, moves or ends.</summary>
    public event Action? Changed;

    public GuidedTour State => tour;

    public bool Active => tour.Active;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/tour.js").AsTask();

    /// <summary>Starts at the first stop and goes to its page.</summary>
    public async Task StartAsync()
    {
        tour.Start();
        // Starting again from Settings during a tour must not add the handler twice
        navigation.LocationChanged -= OnLocationChanged;
        navigation.LocationChanged += OnLocationChanged;
        await CallAsync("listen", self ??= DotNetObjectReference.Create(this));
        await ShowAsync();
    }

    public async Task NextAsync()
    {
        if (!tour.Next())
            return;
        if (tour.Active)
            await ShowAsync();
        else
            await EndedAsync();
    }

    public async Task BackAsync()
    {
        if (tour.Back())
            await ShowAsync();
    }

    /// <summary>Ends the tour from any step, and clears the mark.</summary>
    public async Task SkipAsync()
    {
        if (tour.Skip())
            await EndedAsync();
    }

    [JSInvokable]
    public async Task EscapePressed() => await SkipAsync();

    // Always the page of the step, wherever the person has wandered to
    private async Task ShowAsync()
    {
        var step = tour.Current;
        var path = navigation.ToBaseRelativePath(navigation.Uri).Split('?', '#')[0].TrimEnd('/');
        if (path != step.Route)
            navigation.NavigateTo(step.Route);
        Changed?.Invoke();
        await CallAsync("mark", step.Target);
    }

    private async Task EndedAsync()
    {
        navigation.LocationChanged -= OnLocationChanged;
        await CallAsync("clear");
        await CallAsync("unlisten");
        Changed?.Invoke();
    }

    // Arriving at the step's page by the menu or a link marks its part too
    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        if (tour.Active)
            _ = CallAsync("mark", tour.Current.Target);
    }

    private async Task CallAsync(string method, params object?[] args)
    {
        try
        {
            await (await Module).InvokeVoidAsync(method, args);
        }
        catch (JSException)
        {
            // The mark is a help, so a browser that fails to show it leaves the tour working
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        navigation.LocationChanged -= OnLocationChanged;
        self?.Dispose();
        if (module is { IsCompletedSuccessfully: true })
            await (await module).DisposeAsync();
    }
}
