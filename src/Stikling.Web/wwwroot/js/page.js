// Blazor scrolls to the top after moving to another page, so a link like "help#pests"
// from elsewhere in the app needs a push to reach its section
export function scrollToFragment() {
    const id = decodeURIComponent(location.hash.slice(1));
    if (id)
        document.getElementById(id)?.scrollIntoView({ behavior: "instant" });
}

// Brings an alert into view and moves focus to it, so a screen reader reads it and the
// sticky save button isn't left focused with nothing seeming to happen
export function showAlert(id) {
    const element = document.getElementById(id);
    if (!element)
        return;
    element.scrollIntoView({ behavior: "smooth", block: "center" });
    element.focus({ preventScroll: true });
}

// Brings a section that was just opened on the page into view
export function scrollToId(id) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
}

// The languages the browser is set to, most preferred first, like ["da-DK", "da", "en"]
export function languages() {
    return navigator.languages?.length ? [...navigator.languages] : [navigator.language];
}

// Enter in a search box picks the first result, in the element its aria-controls names, instead of
// submitting the form it's in. Returns something to call dispose() on when the box goes away.
export function enterPicksFirst(input) {
    const onKey = event => {
        if (event.key !== "Enter")
            return;
        event.preventDefault();
        document.getElementById(input.getAttribute("aria-controls"))?.querySelector("button")?.click();
    };
    input.addEventListener("keydown", onKey);
    return { dispose: () => input.removeEventListener("keydown", onKey) };
}
