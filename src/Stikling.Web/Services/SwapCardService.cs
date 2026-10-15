using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Web.Services;

/// <summary>A tile's photo: its id and how it is framed.</summary>
public sealed record SwapCardPhoto(Guid PhotoId, PhotoFrame? Frame);

/// <summary>One tile as JavaScript draws it.</summary>
public sealed record SwapCardTile(string Name, string Detail, string Tone, SwapCardPhoto? Photo)
{
    public static SwapCardTile From(SwapTile tile) =>
        new(tile.Name, tile.Detail, tile.Tone, tile.PhotoId is { } id ? new SwapCardPhoto(id, tile.Frame) : null);
}

/// <summary>One picture of the swap list. <paramref name="Format"/> is "square", "portrait" or "story".</summary>
/// <param name="Line">"1 of 3" when the list is split over several pictures, else null.</param>
public sealed record SwapCardSpec(string Id, string Format, string Title, string? Line, IReadOnlyList<SwapCardTile> Tiles);

/// <param name="Url">An object URL of the picture.</param>
/// <param name="Missing">How many photos on it were not on this device.</param>
public sealed record SwapCardDrawn(string Url, int Missing);

/// <summary>
/// Draws the swap list as pictures and hands them on. The pictures are made and kept in wwwroot/js/swapcard.js;
/// only the words, photo ids and frames cross into JavaScript, and nothing comes back but a picture's address.
/// The size picked is the share card's, so it is remembered by <see cref="ShareCardService"/>.
/// </summary>
public sealed class SwapCardService(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./js/swapcard.js";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>The specs of the pictures for these tiles at that size, one for each picture.</summary>
    public static IReadOnlyList<SwapCardSpec> SpecsFor(IReadOnlyList<SwapTile> tiles, string format)
    {
        var pages = SwapTiles.Split(tiles, format);
        return [.. pages.Select((page, i) => new SwapCardSpec(
            IdFor(i), format, "Available for swap",
            pages.Count > 1 ? $"{i + 1} of {pages.Count}" : null,
            [.. page.Select(SwapCardTile.From)]))];
    }

    public static string IdFor(int index) => $"swap-{index}";

    /// <summary>"stikling-swap-list.jpg" for one picture, "stikling-swap-list-1.jpg" and on for several.</summary>
    public static string FileNameFor(int index, int count) =>
        count == 1 ? "stikling-swap-list.jpg" : $"stikling-swap-list-{index + 1}.jpg";

    /// <summary>Draws the picture on the canvas. Null when a newer drawing on it took over before this one finished.</summary>
    public async Task<SwapCardDrawn?> DrawAsync(ElementReference canvas, SwapCardSpec spec) =>
        await (await Module).InvokeAsync<SwapCardDrawn?>("draw", canvas, spec);

    /// <summary>"shared", "cancelled", "saved" (it couldn't share, so it was saved) or "failed".</summary>
    public async Task<string> ShareAllAsync(IReadOnlyList<string> ids, IReadOnlyList<string> fileNames) =>
        await (await Module).InvokeAsync<string>("shareAll", ids, fileNames);

    /// <summary>"saved" or "failed".</summary>
    public async Task<string> SaveAllAsync(IReadOnlyList<string> ids, IReadOnlyList<string> fileNames) =>
        await (await Module).InvokeAsync<string>("saveEach", ids, fileNames);

    /// <summary>Lets go of the pictures drawn under these ids.</summary>
    public async Task ReleaseAsync(IEnumerable<string> ids)
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.InvokeVoidAsync("release", ids.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
