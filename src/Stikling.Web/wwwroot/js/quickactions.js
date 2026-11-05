// Press and hold on a plant in the Plants list, or right-click it, to open the quick actions sheet. Elements
// that take part carry data-quick-plant with the plant's id. One set of listeners on the document, so the
// list can be redrawn freely. A touch or pen press opens it after half a second; contextmenu covers the
// mouse, the keyboard's menu key and browsers that send it for a long press. Once a press has opened the
// sheet, the click that follows on release is swallowed so it doesn't also open the plant.

const HOLD_MS = 500;
const MOVE_PX = 10;
const SWALLOW_MS = 700;

export function init(dotnet) {
    let timer = null;
    let start = null;
    let opened = false;     // this press has already opened the sheet
    let touching = false;   // a touch or pen press is down
    let swallow = null;     // { element, until }

    const plantOf = target => target?.closest?.("[data-quick-plant]") ?? null;

    const cancel = () => {
        clearTimeout(timer);
        timer = null;
    };

    const open = element => {
        opened = true;
        cancel();
        if (touching) {
            swallow = { element, until: Date.now() + SWALLOW_MS };
        }
        try {
            navigator.vibrate?.(10);
        } catch {
            // Not every browser lets a page vibrate
        }
        dotnet.invokeMethodAsync("Open", element.dataset.quickPlant);
    };

    const onDown = event => {
        opened = false;
        const element = plantOf(event.target);
        if (!element || (event.pointerType !== "touch" && event.pointerType !== "pen")) {
            touching = false;
            return;
        }
        touching = true;
        start = { x: event.clientX, y: event.clientY };
        cancel();
        timer = setTimeout(() => open(element), HOLD_MS);
    };

    const onMove = event => {
        if (timer && start && Math.hypot(event.clientX - start.x, event.clientY - start.y) > MOVE_PX) {
            cancel();
        }
    };

    const onEnd = () => {
        cancel();
    };

    const onContextMenu = event => {
        const element = plantOf(event.target);
        if (!element) {
            return;
        }
        event.preventDefault();
        if (!opened) {
            open(element);
        }
    };

    const onClick = event => {
        if (!swallow) {
            return;
        }
        const { element, until } = swallow;
        if (Date.now() > until) {
            swallow = null;
            return;
        }
        if (element.contains(event.target)) {
            swallow = null;
            event.preventDefault();
            event.stopPropagation();
        }
    };

    const onDragStart = event => {
        if (touching && plantOf(event.target)) {
            event.preventDefault();
        }
    };

    document.addEventListener("pointerdown", onDown);
    document.addEventListener("pointermove", onMove);
    document.addEventListener("pointerup", onEnd);
    document.addEventListener("pointercancel", onEnd);
    document.addEventListener("scroll", onEnd, true);
    document.addEventListener("contextmenu", onContextMenu);
    document.addEventListener("click", onClick, true);
    document.addEventListener("dragstart", onDragStart);

    return {
        dispose() {
            cancel();
            document.removeEventListener("pointerdown", onDown);
            document.removeEventListener("pointermove", onMove);
            document.removeEventListener("pointerup", onEnd);
            document.removeEventListener("pointercancel", onEnd);
            document.removeEventListener("scroll", onEnd, true);
            document.removeEventListener("contextmenu", onContextMenu);
            document.removeEventListener("click", onClick, true);
            document.removeEventListener("dragstart", onDragStart);
        }
    };
}
