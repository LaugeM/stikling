// Blazor scrolls to the top after moving to another page, so a link like "help#pests"
// from elsewhere in the app needs a push to reach its section
export function scrollToFragment() {
    const id = decodeURIComponent(location.hash.slice(1));
    if (id)
        document.getElementById(id)?.scrollIntoView({ behavior: "instant" });
}

// Brings a section that was just opened on the page into view
export function scrollToId(id) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
}
