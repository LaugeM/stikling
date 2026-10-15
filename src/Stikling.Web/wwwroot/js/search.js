// Keys for the Search page and the / key that opens it.

// The box and the results: Arrow down goes from the box into the results, Arrow up and down move between
// them, Arrow up on the first goes back to the box, and Escape in the box empties it. Every row to
// stop on has the class search-stop. Returns something to call dispose() on when the page goes away.
export function attach(input, root) {
    const stops = () => [...root.querySelectorAll(".search-stop")];

    const onBoxKey = event => {
        if (event.key === "ArrowDown") {
            const first = stops()[0];
            if (first) {
                event.preventDefault();
                first.focus();
            }
        } else if (event.key === "Escape" && input.value) {
            input.value = "";
            input.dispatchEvent(new Event("input", { bubbles: true }));
        }
    };

    const onKey = event => {
        if (event.key !== "ArrowDown" && event.key !== "ArrowUp")
            return;
        const list = stops();
        const at = list.indexOf(document.activeElement);
        if (at < 0)
            return;
        event.preventDefault();
        if (event.key === "ArrowDown")
            list[Math.min(at + 1, list.length - 1)].focus();
        else if (at === 0)
            input.focus();
        else
            list[at - 1].focus();
    };

    input.addEventListener("keydown", onBoxKey);
    root.addEventListener("keydown", onKey);
    input.focus();
    return {
        focus: () => input.focus(),
        dispose: () => {
            input.removeEventListener("keydown", onBoxKey);
            root.removeEventListener("keydown", onKey);
        }
    };
}

// The / key opens Search from any page, unless the person is typing somewhere. Shift is allowed, since
// on some keyboards / is a shifted key. Calls OpenSearch on the .NET object, or focuses the box when
// the page is already there.
export function installSlash(dotnet) {
    const onKey = event => {
        if (event.key !== "/" || event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented)
            return;
        const target = event.target;
        if (target?.isContentEditable || target?.closest?.("input, textarea, select, [contenteditable]"))
            return;
        event.preventDefault();
        const box = document.querySelector("[data-search-input]");
        if (box)
            box.focus();
        else
            dotnet.invokeMethodAsync("OpenSearch");
    };
    document.addEventListener("keydown", onKey);
    return { dispose: () => document.removeEventListener("keydown", onKey) };
}
