// Circles and arrows on a photo in the viewer. Four small jobs, none of which keep marks:
//   watchFit    tells the overlay where the photo is drawn inside its box (it is letterboxed), and when that changes
//   layoutLabels keeps each label pill inside the photo
//   attach      the pointer work of marking: drawing, tapping, moving a mark or one of its points
//   followViewport  keeps the viewer above the on-screen keyboard
// The marks themselves live in the component, which gets called back: Picked, Tapped, Dragging, Dropped, Cancelled.
// Points are sent as 0 to 1 across the photo, x from the left edge and y from the top.

const DRAG_START = 8; // px a press has to move to draw
const HANDLE_START = 3; // px a press on a handle has to move, so a tap on it doesn't nudge the point

// Where the photo is drawn inside the container: the picture is fitted into its element, which may be
// bigger than it (object-fit: contain). Relative to the container, in px. Null when there is no photo yet.
function drawnRect(container) {
    const img = container.querySelector("img");
    if (!img)
        return null;
    const box = img.getBoundingClientRect();
    const outer = container.getBoundingClientRect();
    let { left, top, width, height } = box;
    const nw = img.naturalWidth;
    const nh = img.naturalHeight;
    if (nw > 0 && nh > 0 && width > 0 && height > 0) {
        const scale = Math.min(width / nw, height / nh);
        const w = nw * scale;
        const h = nh * scale;
        left += (width - w) / 2;
        top += (height - h) / 2;
        width = w;
        height = h;
    }
    return { left: left - outer.left, top: top - outer.top, width, height };
}

const clamp01 = value => Math.min(1, Math.max(0, value));

function unitPoint(event, container, rect) {
    const outer = container.getBoundingClientRect();
    return {
        x: clamp01((event.clientX - outer.left - rect.left) / rect.width),
        y: clamp01((event.clientY - outer.top - rect.top) / rect.height),
    };
}

// The container is the box around the layer, which holds the photo. Calls Fit(left, top, width, height) on the component now and whenever the photo is drawn somewhere else
export function watchFit(layer, dotNetRef) {
    const container = layer.parentElement;
    let last = "";
    const report = () => {
        const rect = drawnRect(container);
        const [left, top, width, height] = rect ? [rect.left, rect.top, rect.width, rect.height] : [0, 0, 0, 0];
        const key = [left, top, width, height].map(v => v.toFixed(1)).join(",");
        if (key === last)
            return;
        last = key;
        // The layer follows at once; the component works the shapes out again when it hears
        if (rect) {
            layer.style.left = `${left}px`;
            layer.style.top = `${top}px`;
            layer.style.width = `${width}px`;
            layer.style.height = `${height}px`;
        }
        dotNetRef.invokeMethodAsync("Fit", left, top, width, height).catch(() => { });
    };

    const observer = new ResizeObserver(report);
    observer.observe(container);
    const watched = new Set();
    const watchImages = () => {
        for (const img of container.querySelectorAll("img")) {
            if (!watched.has(img)) {
                watched.add(img);
                observer.observe(img);
            }
        }
        report();
    };
    // The photo is swapped for its full size, and a new one loaded, without the container changing
    const mutations = new MutationObserver(watchImages);
    mutations.observe(container, { childList: true, subtree: true });
    container.addEventListener("load", watchImages, true);
    window.addEventListener("resize", report);
    watchImages();

    return {
        dispose() {
            observer.disconnect();
            mutations.disconnect();
            container.removeEventListener("load", watchImages, true);
            window.removeEventListener("resize", report);
        },
    };
}

// Moves each label (data-label) back inside the layer when it sticks out. They are placed by the
// stylesheet from the point they hang on; this only adds the shift (--sx, --sy) that keeps them in.
export function layoutLabels(layer) {
    if (!layer)
        return;
    const box = layer.getBoundingClientRect();
    for (const label of layer.querySelectorAll("[data-label]")) {
        label.style.setProperty("--sx", "0px");
        label.style.setProperty("--sy", "0px");
        const r = label.getBoundingClientRect();
        let sx = 0;
        let sy = 0;
        if (r.right > box.right)
            sx = box.right - r.right;
        if (r.left + sx < box.left)
            sx = box.left - r.left;
        if (r.bottom > box.bottom)
            sy = box.bottom - r.bottom;
        if (r.top + sy < box.top)
            sy = box.top - r.top;
        label.style.setProperty("--sx", `${sx}px`);
        label.style.setProperty("--sy", `${sy}px`);
    }
}

function pointsOf(element) {
    const d = element.dataset;
    return [Number(d.x1), Number(d.y1), Number(d.x2), Number(d.y2)];
}

// The pointer work of marking, on the box that holds the photo and the overlay.
export function attach(stage, dotNetRef) {
    let gesture = null; // { id, sx, sy, start, rect, kind, markId, points, which, dragging }
    let pending = null;
    let frame = 0;
    const send = (method, ...args) => dotNetRef.invokeMethodAsync(method, ...args).catch(() => { });

    const flush = () => {
        frame = 0;
        if (pending)
            send("Dragging", ...pending);
        pending = null;
    };

    const capture = id => {
        try {
            stage.setPointerCapture(id);
        } catch {
            // The drag still works without it
        }
    };

    const onDown = event => {
        if (gesture || (event.pointerType === "mouse" && event.button !== 0))
            return;
        const rect = drawnRect(stage);
        if (!rect || rect.width <= 0 || rect.height <= 0)
            return;

        const start = unitPoint(event, stage, rect);
        const outer = stage.getBoundingClientRect();
        const onPhoto = event.clientX >= outer.left + rect.left && event.clientX <= outer.left + rect.left + rect.width
            && event.clientY >= outer.top + rect.top && event.clientY <= outer.top + rect.top + rect.height;
        gesture = { id: event.pointerId, sx: event.clientX, sy: event.clientY, start, rect, kind: "draw", markId: null, points: null, which: 0, dragging: false };

        const handle = event.target.closest("[data-handle]");
        const mark = event.target.closest("[data-mark]");
        const inside = event.target.closest(".mk-inside") !== null;
        if (!handle && !mark && !onPhoto) {
            // The dark area around the photo: only lets go of a picked mark
            gesture.kind = "outside";
        } else if (handle) {
            gesture.kind = "handle";
            gesture.which = Number(handle.dataset.handle);
            gesture.markId = handle.dataset.mark;
            gesture.points = pointsOf(handle);
        } else if (mark && mark.dataset.picked === "true" && !inside) {
            // The picked mark is moved as a whole by its line or its label
            gesture.kind = "body";
            gesture.markId = mark.dataset.mark;
            gesture.points = pointsOf(mark);
        } else if (mark) {
            // Another mark: a tap picks it, a drag draws a new one over it
            gesture.tapMarkId = mark.dataset.mark;
        }
    };

    const pointsFor = (g, p) => {
        if (g.kind === "handle") {
            const [x1, y1, x2, y2] = g.points;
            return g.which === 1 ? [p.x, p.y, x2, y2] : [x1, y1, p.x, p.y];
        }
        if (g.kind === "body") {
            const [x1, y1, x2, y2] = g.points;
            const dx = Math.min(1 - Math.max(x1, x2), Math.max(-Math.min(x1, x2), p.x - g.start.x));
            const dy = Math.min(1 - Math.max(y1, y2), Math.max(-Math.min(y1, y2), p.y - g.start.y));
            return [x1 + dx, y1 + dy, x2 + dx, y2 + dy];
        }
        return [g.start.x, g.start.y, p.x, p.y];
    };

    const onMove = event => {
        const g = gesture;
        if (!g || event.pointerId !== g.id)
            return;
        if (g.kind === "outside")
            return;
        if (!g.dragging) {
            const limit = g.kind === "handle" ? HANDLE_START : DRAG_START;
            if (Math.hypot(event.clientX - g.sx, event.clientY - g.sy) <= limit)
                return;
            g.dragging = true;
            capture(event.pointerId);
        }
        g.last = pointsFor(g, unitPoint(event, stage, g.rect));
        pending = [g.markId, ...g.last];
        if (!frame)
            frame = requestAnimationFrame(flush);
    };

    const onUp = event => {
        const g = gesture;
        if (!g || event.pointerId !== g.id)
            return;
        gesture = null;
        if (frame) {
            cancelAnimationFrame(frame);
            frame = 0;
        }
        pending = null;

        if (g.kind === "outside") {
            send("Outside");
        } else if (g.dragging) {
            send("Dropped", g.markId, ...g.last);
        } else if (g.kind === "handle") {
            // A tap on a handle does nothing
        } else if (g.tapMarkId) {
            send("Picked", g.tapMarkId);
        } else if (g.kind === "body") {
            send("Picked", g.markId);
        } else {
            send("Tapped", g.start.x, g.start.y, g.rect.width, g.rect.height);
        }
    };

    const onCancel = event => {
        const g = gesture;
        if (!g || event.pointerId !== g.id)
            return;
        gesture = null;
        if (frame) {
            cancelAnimationFrame(frame);
            frame = 0;
        }
        pending = null;
        if (g.dragging)
            send("Cancelled");
    };

    stage.addEventListener("pointerdown", onDown);
    stage.addEventListener("pointermove", onMove);
    stage.addEventListener("pointerup", onUp);
    stage.addEventListener("pointercancel", onCancel);

    return {
        dispose() {
            stage.removeEventListener("pointerdown", onDown);
            stage.removeEventListener("pointermove", onMove);
            stage.removeEventListener("pointerup", onUp);
            stage.removeEventListener("pointercancel", onCancel);
            if (frame)
                cancelAnimationFrame(frame);
        },
    };
}

// Sizes the viewer to what is visible above the on-screen keyboard, so the photo shrinks and the label field
// stays in view. Browsers that resize the page for the keyboard already have that, and nothing is changed there.
export function followViewport(element) {
    const viewer = element?.closest(".viewer");
    const vv = window.visualViewport;
    if (!viewer || !vv)
        return { dispose() { } };

    const reset = () => {
        viewer.style.removeProperty("top");
        viewer.style.removeProperty("bottom");
        viewer.style.removeProperty("height");
    };
    const apply = () => {
        const covered = window.innerHeight - vv.height - vv.offsetTop;
        if (covered < 1 && vv.offsetTop < 1) {
            reset();
            return;
        }
        viewer.style.top = `${vv.offsetTop}px`;
        viewer.style.bottom = "auto";
        viewer.style.height = `${vv.height}px`;
    };

    vv.addEventListener("resize", apply);
    vv.addEventListener("scroll", apply);
    apply();
    return {
        dispose() {
            vv.removeEventListener("resize", apply);
            vv.removeEventListener("scroll", apply);
            reset();
        },
    };
}
