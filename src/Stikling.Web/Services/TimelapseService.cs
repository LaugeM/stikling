using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Web.Services;

/// <summary>One photo of a time-lapse. Label is the date and day count, already formatted. Start and Hold come from <see cref="TimelapsePlan"/>.</summary>
public sealed record TimelapseShotSpec(string PhotoId, PhotoFrame? Frame, string Label, double Start, double Hold);

/// <summary>What to put in a time-lapse. The words come from <see cref="ShareCardText"/>, edited or not.</summary>
/// <param name="Format">"square", "portrait" or "story".</param>
/// <param name="Crossfade">How long the next photo takes to fade in, in seconds.</param>
/// <param name="Total">The length of the video, in seconds.</param>
public sealed record TimelapseSpec(
    string Format,
    string Name,
    string? Latin,
    string? Cultivar,
    string? Line,
    double Crossfade,
    double Total,
    IReadOnlyList<TimelapseShotSpec> Shots);

/// <param name="Small">Photos that only have their small copy on this device.</param>
/// <param name="Missing">Photos with nothing on this device, which can't go in the video.</param>
public sealed record TimelapseAvailability(IReadOnlyList<string> Small, IReadOnlyList<string> Missing);

/// <summary>Takes the progress reports from JavaScript while a video is made.</summary>
public sealed class TimelapseProgress(Action<double> report)
{
    [JSInvokable]
    public void Report(double fraction) => report(fraction);
}

/// <summary>
/// Makes a time-lapse video and hands it on. The photos are read, drawn and encoded in wwwroot/js/timelapse.js
/// and the finished video stays there. Only the words, the timing and a photo id each cross into JavaScript,
/// and nothing comes back but how far it has got and whether it worked.
/// </summary>
public sealed class TimelapseService(IJSRuntime js, DeviceFiles files) : IAsyncDisposable
{
    internal const string ModulePath = "./js/timelapse.js";
    internal const string FormatKey = "share-video-format";
    internal const string SpeedKey = "share-video-speed";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>The size used for the last video on this device. It is kept apart from the card's size, and starts on 9:16.</summary>
    public async Task<string> GetFormatAsync()
    {
        var saved = await files.GetAsync(FormatKey);
        return ShareCardService.Formats.Any(f => f.Value == saved) ? saved! : "story";
    }

    public Task SetFormatAsync(string format) => files.SetAsync(FormatKey, format);

    public async Task<TimelapseSpeed> GetSpeedAsync() => TimelapsePlan.ParseSpeed(await files.GetAsync(SpeedKey));

    public Task SetSpeedAsync(TimelapseSpeed speed) => files.SetAsync(SpeedKey, speed.ToString().ToLowerInvariant());

    /// <summary>True when the browser can share a video through the phone's share menu.</summary>
    public async Task<bool> CanShareAsync() =>
        await (await Module).InvokeAsync<bool>("canShare");

    public async Task<bool> PrefersReducedMotionAsync() =>
        await (await Module).InvokeAsync<bool>("prefersReducedMotion");

    /// <summary>Which of these photos are only small on this device, and which aren't here at all.</summary>
    public async Task<TimelapseAvailability> CheckAsync(IEnumerable<Guid> photoIds) =>
        await (await Module).InvokeAsync<TimelapseAvailability>("check", photoIds.Select(id => id.ToString()).ToList());

    /// <summary>Plays the time-lapse on the canvas in a loop, or shows its first frame. Call again to follow a change.</summary>
    public async Task PreviewAsync(ElementReference canvas, TimelapseSpec spec, bool playing) =>
        await (await Module).InvokeVoidAsync("startPreview", canvas, spec, playing);

    public async Task StopPreviewAsync() =>
        await (await Module).InvokeVoidAsync("stopPreview");

    /// <summary>"done", "cancelled", "unsupported" (this browser can't make videos) or "failed". Progress is a fraction from 0 to 1.</summary>
    public async Task<string> MakeAsync(TimelapseSpec spec, Action<double> progress)
    {
        using var reference = DotNetObjectReference.Create(new TimelapseProgress(progress));
        return await (await Module).InvokeAsync<string>("make", spec, reference);
    }

    public async Task CancelAsync() =>
        await (await Module).InvokeVoidAsync("cancel");

    /// <summary>Drops the video that was made, when something it was made from has changed.</summary>
    public async Task DiscardAsync() =>
        await (await Module).InvokeVoidAsync("discard");

    /// <summary>"shared", "cancelled", "saved" (it couldn't share, so it was saved) or "failed".</summary>
    public async Task<string> ShareAsync(string fileName) =>
        await (await Module).InvokeAsync<string>("share", fileName);

    /// <summary>"saved" or "failed".</summary>
    public async Task<string> SaveAsync(string fileName) =>
        await (await Module).InvokeAsync<string>("save", fileName);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away
            }
        }
    }
}
