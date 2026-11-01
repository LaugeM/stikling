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

// Scrolls the page (the window is what scrolls, the header is sticky) so the marked element sits in the free
// space between the bottom of the header and the top of the tour card. If it fits it goes in the middle of that
// space, and if it is taller its top goes just under the header. Nothing moves if it is already in the space.
function bringIntoView(element) {
    const card = document.querySelector(".tour-card");
    const header = document.querySelector(".app-header");
    const top = header && header.getClientRects().length ? header.getBoundingClientRect().bottom : 0;
    const bottom = card ? card.getBoundingClientRect().top : window.innerHeight;
    const space = bottom - top;
    const box = element.getBoundingClientRect();
    const gap = 8;

    if (space <= 0 || (box.top >= top && box.bottom <= bottom))
        return;

    const target = box.height + gap * 2 <= space
        ? top + (space - box.height) / 2
        : top + gap;
    const calm = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    window.scrollBy({ top: box.top - target, behavior: calm ? "auto" : "smooth" });
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
            // After layout, since the card changes height between steps
            requestAnimationFrame(() => {
                if (mine === attempt)
                    bringIntoView(element);
            });
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
