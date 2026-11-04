// Measuring on the shelf photo page: where on the photo a tap landed, and bringing a dot into view.

const between = value => Math.min(1, Math.max(0, value));

// Where a tap landed in the element, as a share of its width and height from its top left corner.
// It reads the element's place on the screen at the moment of the tap, so it is right while the
// photo is scrolled sideways. Null when the element has no size.
export function pointIn(element, clientX, clientY) {
    const box = element.getBoundingClientRect();
    if (box.width <= 0 || box.height <= 0) return null;
    return { x: between((clientX - box.left) / box.width), y: between((clientY - box.top) / box.height) };
}

// Scrolls the element with this id into view, with its middle in the middle of the box that
// scrolls sideways
export function bringIntoView(id) {
    document.getElementById(id)?.scrollIntoView({ block: "nearest", inline: "center" });
}
