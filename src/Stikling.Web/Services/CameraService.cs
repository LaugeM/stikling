using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>How the camera got on when it was asked to start.</summary>
public enum CameraStart
{
    Ok,

    /// <summary>The person, or the browser, refused access to the camera.</summary>
    Denied,

    /// <summary>There is no camera to use.</summary>
    None,

    Failed
}

/// <summary>
/// Stikling's own camera, which shows a live picture and stores a shot. All of it is done by
/// wwwroot/js/camera.js, and the image goes through photos.js without reaching C#. Only the
/// details of the stored photo come back, to be recorded by <see cref="PhotoService"/>.
/// </summary>
public sealed class CameraService(IJSRuntime js, PhotoService photos) : IAsyncDisposable
{
    private const string ModulePath = "./js/camera.js";

    private Task<IJSObjectReference>? module;
    private bool? available;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>True when the browser lists a camera. Asked once, then remembered.</summary>
    public async Task<bool> IsAvailableAsync()
    {
        if (available is { } known)
            return known;

        try
        {
            available = await (await Module).InvokeAsync<bool>("isAvailable");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            return false;
        }

        return available.Value;
    }

    /// <summary>Starts the live picture in the given &lt;video&gt;.</summary>
    public async Task<CameraStart> StartAsync(ElementReference video)
    {
        var result = await (await Module).InvokeAsync<string>("start", video);
        return result switch
        {
            "ok" => CameraStart.Ok,
            "denied" => CameraStart.Denied,
            "none" => CameraStart.None,
            _ => CameraStart.Failed
        };
    }

    /// <summary>Stores the frame now showing as a photo of the subject.</summary>
    public async Task<PhotoSaveResult> CaptureAsync(ElementReference video, SubjectType subjectType, Guid subjectId)
    {
        var response = await (await Module).InvokeAsync<PhotoService.SaveResponse>("capture", video);
        return await photos.RecordAsync(response, subjectType, subjectId);
    }

    /// <summary>Lets go of the camera.</summary>
    public async Task StopAsync(ElementReference video)
    {
        try
        {
            await (await Module).InvokeVoidAsync("stop", video);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            // The page is going away, and the camera with it
        }
    }

    /// <summary>Clicks a file input, for falling back on the phone's own picker.</summary>
    public async Task ClickInputAsync(string id) =>
        await (await Module).InvokeVoidAsync("clickInput", id);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
