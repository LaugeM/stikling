using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>Measuring on the shelf photo page, which only the browser can do. The work is in wwwroot/js/shelf.js.</summary>
public sealed class ShelfPage(IJSRuntime js) : JsModule(js, "./js/shelf.js")
{
    public sealed record Point(double X, double Y);

    /// <summary>
    /// Where a tap landed on the element, from 0 at its left or top edge to 1 at the right or bottom
    /// edge. Null when the element has no size.
    /// </summary>
    public async Task<Point?> PointInAsync(ElementReference element, double clientX, double clientY) =>
        await (await Module).InvokeAsync<Point?>("pointIn", element, clientX, clientY);

    /// <summary>Scrolls the element with this id into view, also sideways inside a box that scrolls.</summary>
    public async Task BringIntoViewAsync(string elementId) =>
        await (await Module).InvokeVoidAsync("bringIntoView", elementId);
}
