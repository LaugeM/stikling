// The guided tour: marks the part of the page a step is about, and lets Escape skip the tour.
// Nothing is stored. The class tour-mark is only ever on one element at a time.

const waitMs = 3000;
const pollMs = 100;

let attempt = 0;
let timer = 0;
let keyHandler = null;

function visible(element) {
    if (element.getClientRects().length === 0)
        return false;
    const box = element.getBoundingClientRect();
    return box.width > 0 && box.height > 0 && getComputedStyle(element).visibility !== "hidden";
}

function findVisible(key) {
    return [...document.querySelectorAll(`[data-tour="${CSS.escape(key)}"]`)].find(visible) ?? null;
}

// Removes the mark and stops looking for one
export function clear() {
    attempt++;
    clearTimeout(timer);
    for (const element of document.querySelectorAll(".tour-mark"))
        element.classList.remove("tour-mark");
}

// Marks the first visible element with data-tour="key". Pages load their data after they appear, so it
// keeps looking for about three seconds, and marks nothing if the element never turns up.
export function mark(key) {
    clear();
    if (!key)
        return;

    const mine = attempt;
    const started = performance.now();
    const look = () => {
        if (mine !== attempt)
            return;
        const element = findVisible(key);
        if (element) {
            element.classList.add("tour-mark");
            const calm = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            element.scrollIntoView({ block: "center", behavior: calm ? "auto" : "smooth" });
            return;
        }
        if (performance.now() - started < waitMs)
            timer = setTimeout(look, pollMs);
    };
    look();
}

// Escape skips the tour on a computer. It leaves alone an Escape that something else used, like an open sheet.
export function listen(reference) {
    unlisten();
    keyHandler = event => {
        if (event.key !== "Escape" || event.defaultPrevented || document.querySelector("dialog[open]"))
            return;
        reference.invokeMethodAsync("EscapePressed");
    };
    document.addEventListener("keydown", keyHandler);
}

export function unlisten() {
    if (keyHandler)
        document.removeEventListener("keydown", keyHandler);
    keyHandler = null;
}
