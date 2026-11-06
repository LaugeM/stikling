// The app's keyboard shortcuts, all from one listener on the document. Nothing here fires while someone is typing,
// with Ctrl, Alt or Meta held, or behind something open on top of the page. Escape isn't here: sheets are native
// dialogs, which close on it themselves.

// What the arrow keys move between. They only act when one of these already has focus, so with nothing
// focused they scroll the page as usual, and at the end of a list they do too, so focus is never held.
const STOPS = "a.item-card, a.plant-tile, a.plant-name";

const isTyping = target => target?.isContentEditable
    || target?.closest?.("input, textarea, select, [contenteditable]");

const somethingOnTop = () => document.querySelector("dialog[open], [aria-modal='true']");

// Going to another page from here would lose what is on this one
const unsavedWork = () => document.querySelector("main form")
    || [...document.querySelectorAll("main textarea")].some(box => box.value);

// The nearest stop in that direction. Up and down stay in the same column, so a grid moves straight down and a
// page with two columns doesn't jump across. Left and right stay on the same row.
function neighbour(from, key) {
    const at = from.getBoundingClientRect();
    let best = null;
    let bestGap = Infinity;
    for (const stop of document.querySelectorAll(`main :is(${STOPS})`)) {
        if (stop === from || !stop.getClientRects().length)
            continue;
        const r = stop.getBoundingClientRect();
        const sameColumn = r.left < at.right && r.right > at.left;
        const sameRow = r.top < at.bottom && r.bottom > at.top;
        const gap = key === "ArrowDown" && sameColumn ? r.top - at.bottom
            : key === "ArrowUp" && sameColumn ? at.top - r.bottom
            : key === "ArrowRight" && sameRow ? r.left - at.right
            : key === "ArrowLeft" && sameRow ? at.left - r.right
            : -Infinity;
        if (gap > -1 && gap < bestGap) {
            best = stop;
            bestGap = gap;
        }
    }
    return best;
}

function moveFocus(event) {
    const from = document.activeElement;
    if (!from?.matches?.(STOPS) || !from.closest("main") || event.shiftKey)
        return;
    const next = neighbour(from, event.key);
    if (!next)
        return;
    event.preventDefault();
    next.focus({ preventScroll: true });
    next.scrollIntoView({ block: "nearest", inline: "nearest" });
}

// W only acts where one plant is clearly meant: the plant whose page this is, or the one focused in the Plants list.
// Either way it opens the care form on watering, and nothing is logged until that form is saved.
function water() {
    const row = document.activeElement?.closest?.("[data-quick-plant]");
    if (row) {
        row.dispatchEvent(new CustomEvent("stikling-quick-water", { bubbles: true }));
        return true;
    }
    const button = document.querySelector("[data-shortcut='water']");
    button?.click();
    return !!button;
}

// Calls OpenSearch, NewPlant and ShowShortcuts on the .NET object.
export function install(dotnet) {
    const onKey = event => {
        if (event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented || event.isComposing)
            return;
        // composedPath, since a sign-in form in a shadow root shows up here as its host
        const target = event.composedPath?.()[0] ?? event.target;
        if (isTyping(target) || somethingOnTop())
            return;

        // / and ? are shifted keys on many keyboards, so Shift is allowed for them
        if (event.key === "/") {
            const box = document.querySelector("[data-search-input]");
            if (box) {
                event.preventDefault();
                box.focus();
            } else if (!unsavedWork()) {
                event.preventDefault();
                dotnet.invokeMethodAsync("OpenSearch");
            }
        } else if (event.key === "?") {
            event.preventDefault();
            dotnet.invokeMethodAsync("ShowShortcuts");
        } else if (event.key.startsWith("Arrow")) {
            moveFocus(event);
        } else if (!event.shiftKey && !event.repeat) {
            const letter = event.key.toLowerCase();
            if (letter === "n" && !unsavedWork()) {
                event.preventDefault();
                dotnet.invokeMethodAsync("NewPlant");
            } else if (letter === "w" && water()) {
                event.preventDefault();
            }
        }
    };
    document.addEventListener("keydown", onKey);
    return { dispose: () => document.removeEventListener("keydown", onKey) };
}
