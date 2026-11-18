// The Plants and Propagations lists on a large screen: a click on a plant or propagation opens it in a pane
// beside the list instead of going to its page. One click listener on the page's root element, in the bubbling
// phase so a press-and-hold that was swallowed on the way down never gets here, and before Blazor's own link
// handling on the document, which leaves a click alone once it has been prevented.

// The same width as the CSS that lays the pane out
const wide = () => window.matchMedia("(min-width: 96rem)").matches;

const somethingOnTop = () => document.querySelector("dialog[open], [aria-modal='true']");

const isTyping = target => target?.isContentEditable
    || target?.closest?.("input, textarea, select, [contenteditable]");

export const isWide = wide;

// prefix is "plants" or "propagations". dotnet.Open(id) and dotnet.Close() are called on the .NET side.
// Returns something to call dispose() on. setEnabled(false) turns the clicks back into ordinary navigation.
export function attach(root, dotnet, prefix) {
    let enabled = false;
    const pattern = new RegExp(`^${prefix}/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$`, "i");

    const onClick = event => {
        if (!enabled || event.defaultPrevented || event.button !== 0
            || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !wide())
            return;
        const link = event.target?.closest?.("a[href]");
        if (!link || !root.contains(link) || (link.target && link.target !== "_self") || link.hasAttribute("download")
            || link.closest(".side-pane-head")) // the pane's own Open page link goes to the full page
            return;
        const match = pattern.exec(link.getAttribute("href") ?? "");
        if (!match)
            return;
        event.preventDefault();
        dotnet.invokeMethodAsync("Open", match[1].toLowerCase());
    };

    // Escape closes the pane, unless a sheet, a dialog or a field has a use for it
    const onKey = event => {
        if (!enabled || event.key !== "Escape" || event.defaultPrevented || event.isComposing
            || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey)
            return;
        const target = event.composedPath?.()[0] ?? event.target;
        const pane = root.querySelector(".side-pane");
        if (!pane || !pane.getClientRects().length || isTyping(target) || somethingOnTop())
            return;
        event.preventDefault();
        dotnet.invokeMethodAsync("Close");
    };

    root.addEventListener("click", onClick);
    document.addEventListener("keydown", onKey);
    return {
        setEnabled: value => { enabled = value; },
        // A different plant opened: bring the top of the pane back into view if the page was scrolled past it
        showPane: () => {
            const pane = root.querySelector(".side-pane");
            if (pane && pane.getClientRects().length && pane.getBoundingClientRect().top < 0)
                pane.scrollIntoView({ block: "start" });
        },
        dispose: () => {
            root.removeEventListener("click", onClick);
            document.removeEventListener("keydown", onKey);
        }
    };
}
