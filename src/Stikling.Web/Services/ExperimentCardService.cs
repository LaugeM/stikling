using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Strings = Stikling.Web.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Web.Services;

/// <summary>One medium as JavaScript draws it. <paramref name="Days"/> is the average days to root, or null for no bar.</summary>
public sealed record ExperimentCardRow(string Name, string Result, int? Days, SwapCardPhoto? Photo);

/// <summary>The picture of one experiment's result. <paramref name="Format"/> is "square", "portrait" or "story".</summary>
/// <param name="More">"and 2 more" when not every medium fits at this size, else null.</param>
public sealed record ExperimentCardSpec(string Id, string Format, string Kicker, string Title, string? More, IReadOnlyList<ExperimentCardRow> Rows);

/// <summary>
/// Draws an experiment's result as a picture and hands it on. The picture is made and kept in wwwroot/js/experimentcard.js;
/// only the words, photo ids and frames cross into JavaScript, and nothing comes back but the picture's address.
/// The size picked is the share card's, so it is remembered by <see cref="ShareCardService"/>.
/// </summary>
public sealed class ExperimentCardService(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./js/experimentcard.js";
    internal const string PictureId = "experiment-result";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>How many mediums fit on a picture of that size.</summary>
    public static int MaxRows(string format) => format switch
    {
        "square" => 5,
        "portrait" => 7,
        _ => 8,
    };

    /// <summary>Whether the experiment has two mediums to compare, which is when it can be shared.</summary>
    public static bool CanShare(ExperimentGroup group) =>
        group.Entries.Select(e => e.Propagation.Medium).Distinct().Count() >= 2;

    public static ExperimentCardSpec SpecFor(ExperimentGroup group, ExperimentResult result, string format)
    {
        var seeds = group.Entries.All(e => e.Propagation.Type == PropagationType.Seed);
        var kicker = (seeds, result.StillRunning) switch
        {
            (true, true) => Strings.Propagations.ExpCardKickerSproutedSoFar,
            (true, false) => Strings.Propagations.ExpCardKickerSprouted,
            (false, true) => Strings.Propagations.ExpCardKickerRootedSoFar,
            _ => Strings.Propagations.ExpCardKickerRooted,
        };

        var shown = result.Rows.Take(MaxRows(format)).ToList();
        var left = result.Rows.Count - shown.Count;
        return new ExperimentCardSpec(PictureId, format, kicker, result.Name,
            left > 0 ? F(Strings.Propagations.ExpCardMore, left) : null,
            [.. shown.Select(row => new ExperimentCardRow(Labels.For(row.Medium), ResultText(row), row.AverageDays,
                row.PhotoId is { } id ? new SwapCardPhoto(id, row.Frame) : null))]);
    }

    // "4 of 4, 21 days", or "0 of 4" when no batch has rooted
    private static string ResultText(ExperimentResultRow row) => row.AverageDays switch
    {
        null => F(Strings.Propagations.ExpCardResult, row.Rooted, row.Total),
        1 => F(Strings.Propagations.ExpCardResultDays_One, row.Rooted, row.Total, 1),
        var days => F(Strings.Propagations.ExpCardResultDays_Other, row.Rooted, row.Total, days),
    };

    /// <summary>"stikling-alocasia-corm-test-result.jpg"</summary>
    public static string FileNameFor(string name) => ShareCardService.FileNameFor(name, ShareFileKind.ExperimentResult);

    /// <summary>Draws the picture on the canvas. Null when a newer drawing on it took over before this one finished.</summary>
    public async Task<SwapCardDrawn?> DrawAsync(ElementReference canvas, ExperimentCardSpec spec) =>
        await (await Module).InvokeAsync<SwapCardDrawn?>("draw", canvas, spec);

    /// <summary>"shared", "cancelled", "saved" (it couldn't share, so it was saved) or "failed".</summary>
    public async Task<string> ShareAsync(string fileName) =>
        await (await Module).InvokeAsync<string>("share", PictureId, fileName);

    /// <summary>"saved" or "failed".</summary>
    public async Task<string> SaveAsync(string fileName) =>
        await (await Module).InvokeAsync<string>("save", PictureId, fileName);

    /// <summary>Lets go of the picture drawn.</summary>
    public async Task ReleaseAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.InvokeVoidAsync("release", new[] { PictureId });
    }

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
