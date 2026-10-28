using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Year;

namespace Stikling.Web.Services;

/// <summary>
/// What to draw on one card of "Your year": the card's words and photos, in the size picked. For the
/// collage, <paramref name="Collage"/> is the photos that fit that size, already cut.
/// </summary>
/// <param name="Format">"square", "portrait" or "story".</param>
public sealed record YearCardSpec(
    string Id,
    YearCardKind Kind,
    string Tone,
    string? Kicker,
    string Title,
    string? Latin,
    string? Cultivar,
    string? Line,
    IReadOnlyList<YearFigure> Figures,
    YearCardPhoto? Photo,
    YearCardPhoto? Before,
    YearCardPhoto? After,
    string? BeforeLabel,
    string? AfterLabel,
    IReadOnlyList<YearCardPhoto> Collage,
    string Format)
{
    public static YearCardSpec From(YearCard card, string format, IReadOnlyList<YearCardPhoto>? collage = null) =>
        new(card.Id, card.Kind, card.Tone, card.Kicker, card.Title, card.Latin, card.Cultivar, card.Line, card.Figures,
            card.Photo, card.Before, card.After, card.BeforeLabel, card.AfterLabel, collage ?? card.Collage, format);
}

/// <summary>A photo's box on a collage card, as fractions of the card.</summary>
public sealed record YearCardCell(double X, double Y, double W, double H);

/// <param name="Url">An object URL of the card's picture.</param>
/// <param name="Missing">How many photos on the card weren't on this device.</param>
/// <param name="Cells">The collage's photos, in order. Null on any other card.</param>
public sealed record YearCardDrawn(string Url, int Missing, IReadOnlyList<YearCardCell>? Cells);

/// <summary>Which photos are only small on this device, and which aren't here at all.</summary>
public sealed record YearPhotoAvailability(IReadOnlyList<string> Small, IReadOnlyList<string> Missing);

/// <summary>
/// Draws the cards of "Your year" and hands them on. The pictures are made and kept in wwwroot/js/yearcards.js;
/// only the words and photo ids cross into JavaScript, and nothing comes back but a picture's address.
/// The size picked stays on this device.
/// </summary>
public sealed class YearCardService(IJSRuntime js, DeviceFiles files) : IAsyncDisposable
{
    internal const string ModulePath = "./js/yearcards.js";
    internal const string FormatKey = "year-format";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>The size used last on this device. One that doesn't exist any more comes back as 9:16.</summary>
    public async Task<string> GetFormatAsync()
    {
        var saved = await files.GetAsync(FormatKey);
        return ShareCardService.Formats.Any(f => f.Value == saved) ? saved! : "story";
    }

    public Task SetFormatAsync(string format) => files.SetAsync(FormatKey, format);

    /// <summary>How many photos a collage shows at that size: 3 by 3 on a square, 3 by 4 on the others.</summary>
    public static int CollageShown(string format) => format == "square" ? 9 : 12;

    /// <summary>Draws the card on the canvas. Null when a newer drawing on it took over before this one finished.</summary>
    public async Task<YearCardDrawn?> DrawAsync(ElementReference canvas, YearCardSpec spec) =>
        await (await Module).InvokeAsync<YearCardDrawn?>("draw", canvas, spec);

    public async Task<YearPhotoAvailability> CheckAsync(IEnumerable<Guid> photoIds) =>
        await (await Module).InvokeAsync<YearPhotoAvailability>("check", photoIds.Select(id => id.ToString()).ToList());

    /// <summary>True when the browser can share this many pictures in one go.</summary>
    public async Task<bool> CanShareAllAsync(int count) =>
        await (await Module).InvokeAsync<bool>("canShareAll", count);

    /// <summary>"shared", "cancelled", "saved" (it couldn't share, so it was saved) or "failed".</summary>
    public async Task<string> ShareAsync(string id, string fileName) =>
        await (await Module).InvokeAsync<string>("share", id, fileName);

    /// <summary>"saved" or "failed".</summary>
    public async Task<string> SaveAsync(string id, string fileName) =>
        await (await Module).InvokeAsync<string>("save", id, fileName);

    /// <summary>"shared", "cancelled", "saved" or "failed".</summary>
    public async Task<string> ShareAllAsync(IReadOnlyList<string> ids, IReadOnlyList<string> fileNames) =>
        await (await Module).InvokeAsync<string>("shareAll", ids, fileNames);

    /// <summary>Lets the front card on the stage be swiped. The page's <c>Swipe(int direction, double moved)</c> is called.</summary>
    public async Task<IJSObjectReference> AttachDeckAsync<T>(ElementReference stage, DotNetObjectReference<T> page) where T : class =>
        await (await Module).InvokeAsync<IJSObjectReference>("attachDeck", stage, page);

    /// <summary>Lets go of the pictures drawn under these ids.</summary>
    public async Task ReleaseAsync(IEnumerable<string> ids)
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.InvokeVoidAsync("release", ids.ToArray());
    }

    /// <summary>"stikling-2026-plants-added.jpg".</summary>
    public static string FileNameFor(int year, string id) => $"stikling-{year}-{id}.jpg";

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
