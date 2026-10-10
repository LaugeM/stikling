using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Web.Services;

/// <summary>What to draw on a card. The words come from <see cref="ShareCardText"/>, edited or not.</summary>
/// <param name="Format">"square", "portrait" or "story".</param>
/// <param name="Tone">"plant" or "propagation", which sets the colour of a card without a photo.</param>
/// <param name="PhotoId">The photo to put on the card, or null for the card built around the number.</param>
/// <param name="Pair">Two photos for the before and after card. When set, <paramref name="PhotoId"/> and <paramref name="Frame"/> are not used.</param>
public sealed record ShareCardSpec(
    string Format,
    string Tone,
    string Name,
    string? Latin,
    string? Cultivar,
    string? Line,
    ShareFigure? Figure,
    string? PhotoId,
    PhotoFrame? Frame,
    ShareCardPair? Pair = null);

/// <summary>One side of a before and after card. Label is the date, already formatted.</summary>
public sealed record ShareCardSide(string PhotoId, PhotoFrame? Frame, string Label);

/// <summary>The older photo is first and is drawn first, whatever order the person picked them in.</summary>
public sealed record ShareCardPair(ShareCardSide Before, ShareCardSide After);

/// <param name="Photo">Whether the photo made it onto the card. False when it is missing from this device.</param>
/// <param name="Small">True when only the small version of the photo is on this device, so it looks soft on the card.</param>
/// <param name="Photos">Before and after card only: the ids of the photos that made it onto the card.</param>
/// <param name="SmallPhotos">Before and after card only: the ids of the photos that are only small on this device.</param>
public sealed record ShareCardDrawn(bool Photo, bool Small, IReadOnlyList<string>? Photos = null, IReadOnlyList<string>? SmallPhotos = null);

/// <summary>What a shared file is, which decides the end of its name.</summary>
public enum ShareFileKind
{
    Photo,
    BeforeAfter,
    Timelapse,
}

/// <summary>
/// Draws a share card and hands it on, and shares or copies the address of a page. The picture is made and kept in wwwroot/js/sharecard.js;
/// only the words and a photo id cross into JavaScript, and nothing comes back but whether it worked.
/// </summary>
public sealed class ShareCardService(IJSRuntime js, DeviceFiles files) : IAsyncDisposable
{
    internal const string ModulePath = "./js/sharecard.js";
    internal const string FormatKey = "share-format";

    public static readonly IReadOnlyList<(string Value, string Label)> Formats =
    [
        ("square", "Square"),
        ("portrait", "4:5"),
        ("story", "9:16"),
    ];

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>The format used last on this device. A format that doesn't exist any more comes back as square.</summary>
    public async Task<string> GetFormatAsync()
    {
        var saved = await files.GetAsync(FormatKey);
        return Formats.Any(f => f.Value == saved) ? saved! : "square";
    }

    public Task SetFormatAsync(string format) => files.SetAsync(FormatKey, format);

    /// <summary>True when the browser can share a picture through the phone's share menu.</summary>
    public async Task<bool> CanShareAsync() =>
        await (await Module).InvokeAsync<bool>("canShare");

    /// <summary>True when the browser has a share menu for a link.</summary>
    public async Task<bool> CanShareLinkAsync() =>
        await (await Module).InvokeAsync<bool>("canShareLink");

    /// <summary>"shared", "cancelled" or "failed".</summary>
    public async Task<string> ShareLinkAsync(string title, string url) =>
        await (await Module).InvokeAsync<string>("shareLink", title, url);

    /// <summary>True when the text was copied.</summary>
    public async Task<bool> CopyTextAsync(string text) =>
        await (await Module).InvokeAsync<bool>("copyText", text);

    /// <summary>Draws the card on the canvas. Null when a newer drawing took over before this one finished.</summary>
    public async Task<ShareCardDrawn?> DrawAsync(ElementReference canvas, ShareCardSpec spec) =>
        await (await Module).InvokeAsync<ShareCardDrawn?>("draw", canvas, spec);

    /// <summary>"shared", "cancelled", "saved" (it couldn't share, so it was saved) or "failed".</summary>
    public async Task<string> ShareAsync(ElementReference canvas, string fileName) =>
        await (await Module).InvokeAsync<string>("share", canvas, fileName);

    /// <summary>"saved" or "failed".</summary>
    public async Task<string> SaveAsync(ElementReference canvas, string fileName) =>
        await (await Module).InvokeAsync<string>("save", canvas, fileName);

    /// <summary>
    /// A file name made from the card's name: "stikling-monstera-cutting.jpg", "stikling-monstera-before-after.jpg"
    /// for a before and after card, or "stikling-monstera-timelapse.mp4" for a time-lapse.
    /// </summary>
    public static string FileNameFor(string name, ShareFileKind kind = ShareFileKind.Photo)
    {
        var slug = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        slug = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > 40)
            slug = slug[..40].TrimEnd('-');
        if (kind == ShareFileKind.Timelapse)
            return slug.Length == 0 ? "stikling-timelapse.mp4" : $"stikling-{slug}-timelapse.mp4";
        var tail = kind == ShareFileKind.BeforeAfter ? "-before-after" : "";
        return slug.Length == 0 ? $"stikling-card{tail}.jpg" : $"stikling-{slug}{tail}.jpg";
    }

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
