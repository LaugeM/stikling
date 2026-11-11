// The gestures of the compare view: swiping a half to step it to another photo, and dragging the line
// of the slider. Nothing is kept here. Each gesture calls back into the component, which owns the state:
// Swiped(half, step) with -1 for an older photo and 1 for a newer one, and SliderTo(percent).
// Vertical movement is left alone, and only the main mouse button counts.

export function attach(stage, dotNetRef) {
    // The page behind stays where it was while the view is open
    const root = document.documentElement;
    const overflowBefore = root.style.overflow;
    root.style.overflow = "hidden";
    let pointer = null; // { id, x, y, half, slider, dragging }
    let pending = null; // the slider position waiting for the next frame
    let frame = 0;

    const sendSlider = percent => {
        pending = percent;
        if (frame)
            return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            if (pending !== null)
                dotNetRef.invokeMethodAsync("SliderTo", pending);
            pending = null;
        });
    };

    const percentAt = (box, clientX) => {
        const rect = box.getBoundingClientRect();
        if (rect.width <= 0)
            return 50;
        return Math.min(100, Math.max(0, ((clientX - rect.left) / rect.width) * 100));
    };

    // The tap that ends a drag isn't a tap on whatever is under the finger
    const swallowNextClick = () => {
        const swallow = e => e.stopPropagation();
        stage.addEventListener("click", swallow, { capture: true, once: true });
        setTimeout(() => stage.removeEventListener("click", swallow, { capture: true }), 0);
    };

    const capture = id => {
        try {
            stage.setPointerCapture(id);
        } catch {
            // The drag still works without it
        }
    };

    const onDown = event => {
        if (event.pointerType === "mouse" && event.button !== 0)
            return;
        const box = event.target.closest("[data-slider-box]");
        if (box) {
            // A pill is a button, so it waits to see if this becomes a drag. Anywhere else the line moves at once.
            const onButton = event.target.closest("button") !== null;
            pointer = { id: event.pointerId, x: event.clientX, y: event.clientY, box, dragging: !onButton };
            if (pointer.dragging) {
                capture(event.pointerId);
                sendSlider(percentAt(box, event.clientX));
            }
            return;
        }
        const half = event.target.closest("[data-half]");
        if (half)
            pointer = { id: event.pointerId, x: event.clientX, y: event.clientY, half: Number(half.dataset.half), dragging: false };
    };

    const onMove = event => {
        if (!pointer || event.pointerId !== pointer.id)
            return;
        const dx = event.clientX - pointer.x;
        const dy = event.clientY - pointer.y;
        if (pointer.box) {
            if (!pointer.dragging) {
                if (Math.hypot(dx, dy) < 6)
                    return;
                pointer.dragging = true;
                capture(event.pointerId);
            }
            sendSlider(percentAt(pointer.box, event.clientX));
            return;
        }
        if (!pointer.dragging) {
            if (Math.abs(dy) > 10 && Math.abs(dy) > Math.abs(dx)) {
                pointer = null;
                return;
            }
            if (Math.abs(dx) < 8 || Math.abs(dx) < Math.abs(dy) * 1.2)
                return;
            pointer.dragging = true;
            capture(event.pointerId);
        }
    };

    const onUp = event => {
        if (!pointer || event.pointerId !== pointer.id)
            return;
        const { dragging, x, half, box } = pointer;
        pointer = null;
        if (!dragging)
            return;
        swallowNextClick();
        if (box)
            return;
        const dx = event.clientX - x;
        if (Math.abs(dx) > 40)
            dotNetRef.invokeMethodAsync("Swiped", half, dx > 0 ? -1 : 1);
    };

    const onCancel = event => {
        if (pointer && event.pointerId === pointer.id)
            pointer = null;
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
            root.style.overflow = overflowBefore;
        },
    };
}

// Scrolls the strip of photos sideways so the marked tile is in view, without moving anything else
export function reveal(strip) {
    const tile = strip?.querySelector("[data-current]");
    if (!tile)
        return;
    const left = tile.offsetLeft - (strip.clientWidth - tile.offsetWidth) / 2;
    strip.scrollLeft = Math.max(0, left);
}
