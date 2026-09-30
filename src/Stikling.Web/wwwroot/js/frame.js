// Framing a photo: drag it around inside its crop and zoom with a pinch, the mouse wheel, the
// slider or the keyboard. The photo is placed by the "framed" class in app.css, the same way as
// everywhere else it's cropped, so this only moves the numbers it reads. Blazor reads them back
// when saving.

const MAX_ZOOM = 4;
const clamp = (value, min, max) => Math.min(max, Math.max(min, value));

// aspect is the photo's width over its height, known from its details before the image has loaded
export function attach(box, img, slider, x, y, zoom, aspect) {
    const state = { x, y, zoom, pointers: new Map(), pinch: null };
    box.stiklingFrame = state;

    // The photo's size in the crop at this zoom, in pixels
    const size = () => {
        const bw = box.clientWidth, bh = box.clientHeight;
        return { bw, bh, w: state.zoom * Math.max(bw, bh * aspect), h: state.zoom * Math.max(bw / aspect, bh) };
    };

    // The middle of the crop can only go as far as the photo's edges, so going further does nothing
    const keepInside = () => {
        const { bw, bh, w, h } = size();
        const edgeX = bw / (2 * w), edgeY = bh / (2 * h);
        state.x = clamp(state.x, Math.min(0.5, edgeX), Math.max(0.5, 1 - edgeX));
        state.y = clamp(state.y, Math.min(0.5, edgeY), Math.max(0.5, 1 - edgeY));
    };

    const draw = () => {
        img.classList.add("framed");
        img.style.setProperty("--fx", state.x);
        img.style.setProperty("--fy", state.y);
        img.style.setProperty("--z", state.zoom);
        img.style.setProperty("--a", aspect);
        slider.value = String(state.zoom);
    };
    state.draw = draw;

    // Dragging the photo right brings more of its left side into view, so the middle moves left
    const move = (dx, dy) => {
        const { w, h } = size();
        state.x -= dx / w;
        state.y -= dy / h;
        keepInside();
        draw();
    };

    const zoomTo = value => {
        state.zoom = clamp(value, 1, MAX_ZOOM);
        keepInside();
        draw();
    };

    const spread = () => {
        const [a, b] = [...state.pointers.values()];
        return Math.hypot(a.x - b.x, a.y - b.y);
    };

    box.addEventListener("pointerdown", event => {
        box.setPointerCapture(event.pointerId);
        state.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
        state.pinch = state.pointers.size === 2 ? { spread: spread(), zoom: state.zoom } : null;
    });

    box.addEventListener("pointermove", event => {
        const last = state.pointers.get(event.pointerId);
        if (!last)
            return;

        const dx = event.clientX - last.x, dy = event.clientY - last.y;
        state.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });

        if (state.pinch && state.pointers.size === 2)
            zoomTo(state.pinch.zoom * spread() / Math.max(1, state.pinch.spread));
        else if (state.pointers.size === 1)
            move(dx, dy);
    });

    const release = event => {
        state.pointers.delete(event.pointerId);
        state.pinch = null;
    };
    box.addEventListener("pointerup", release);
    box.addEventListener("pointercancel", release);

    box.addEventListener("wheel", event => {
        event.preventDefault();
        zoomTo(state.zoom * Math.exp(-event.deltaY * 0.002));
    }, { passive: false });

    box.addEventListener("keydown", event => {
        const step = 12;
        switch (event.key) {
            case "ArrowLeft": move(step, 0); break;
            case "ArrowRight": move(-step, 0); break;
            case "ArrowUp": move(0, step); break;
            case "ArrowDown": move(0, -step); break;
            case "+": case "=": zoomTo(state.zoom * 1.1); break;
            case "-": zoomTo(state.zoom / 1.1); break;
            default: return;
        }
        event.preventDefault();
    });

    slider.addEventListener("input", () => zoomTo(Number(slider.value)));

    keepInside();
    draw();
}

export function set(box, x, y, zoom) {
    const state = box?.stiklingFrame;
    if (!state)
        return;
    Object.assign(state, { x, y, zoom });
    state.draw();
}

export function get(box) {
    const state = box?.stiklingFrame;
    return state ? { x: state.x, y: state.y, zoom: state.zoom } : null;
}
