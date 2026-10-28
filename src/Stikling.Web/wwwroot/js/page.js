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

// Settings on a phone is one long page, so a link to a section scrolls to it. From 64rem only one
// section shows at a time and there is nothing to scroll to.
export function scrollToSectionOnPhone(id) {
    if (matchMedia("(min-width: 64rem)").matches)
        return;
    document.getElementById(id)?.scrollIntoView({ behavior: "instant", block: "start" });
}

// The languages the browser is set to, most preferred first, like ["da-DK", "da", "en"]
export function languages() {
    return navigator.languages?.length ? [...navigator.languages] : [navigator.language];
}

// Enter in a search box picks the first result, in the element its aria-controls names, instead of
// submitting the form it's in. Returns something to call dispose() on when the box goes away.
// The box is null when it left the page before this ran.
export function enterPicksFirst(input) {
    if (!input)
        return { dispose: () => {} };
    const onKey = event => {
        if (event.key !== "Enter")
            return;
        event.preventDefault();
        document.getElementById(input.getAttribute("aria-controls"))?.querySelector("button")?.click();
    };
    input.addEventListener("keydown", onKey);
    return { dispose: () => input.removeEventListener("keydown", onKey) };
}

// Scrolls to an element and fills it for a moment (the is-found class), for a result opened from Search.
// The page may still be drawing the element, so it looks again for a few seconds.
export function showFound(id) {
    let tries = 0;
    const look = () => {
        const element = document.getElementById(id);
        if (!element) {
            if (++tries < 30)
                setTimeout(look, 100);
            return;
        }
        element.scrollIntoView({ behavior: "instant", block: "center" });
        element.classList.remove("is-found");
        void element.offsetWidth;
        element.classList.add("is-found");
    };
    look();
}
