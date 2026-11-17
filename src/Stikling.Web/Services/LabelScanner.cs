using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>
/// Reads QR codes from the camera, for the Scan a label page. The camera and the reading are in
/// wwwroot/js/scan.js; each code that is read comes back through the callback given to
/// <see cref="StartAsync"/>.
/// </summary>
public sealed class LabelScanner(IJSRuntime js) : JsModule(js, "./js/scan.js")
{
    private DotNetObjectReference<Target>? target;

    /// <summary>Starts the camera in the video and reads codes from it.</summary>
    public async Task<CameraStart> StartAsync(ElementReference video, Func<string, Task<bool>> onRead)
    {
        target?.Dispose();
        target = DotNetObjectReference.Create(new Target(onRead));
        var result = await (await Module).InvokeAsync<string>("start", video, target);
        return result switch
        {
            "ok" => CameraStart.Ok,
            "denied" => CameraStart.Denied,
            "none" => CameraStart.None,
            _ => CameraStart.Failed
        };
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
        target?.Dispose();
        target = null;
    }

    /// <summary>Goes back in the browser's history, and says whether there was anywhere to go.</summary>
    public async Task<bool> GoBackAsync() =>
        await (await Module).InvokeAsync<bool>("goBack");

    public sealed class Target(Func<string, Task<bool>> onRead)
    {
        /// <summary>True when the text opened something, so the camera can stop.</summary>
        [JSInvokable]
        public Task<bool> OnRead(string text) => onRead(text);
    }
}
