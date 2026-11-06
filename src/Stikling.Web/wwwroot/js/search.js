// Keys for the Search page. The / key that opens it is in shortcuts.js.

// The box and the results: Arrow down goes from the box into the results, Arrow up and down move between
// them, Arrow up on the first goes back to the box, and Escape empties the box, from a result too. Every row to
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
        if (event.key !== "ArrowDown" && event.key !== "ArrowUp" && event.key !== "Escape")
            return;
        const list = stops();
        const at = list.indexOf(document.activeElement);
        if (at < 0)
            return;
        event.preventDefault();
        if (event.key === "Escape") {
            input.focus();
            input.value = "";
            input.dispatchEvent(new Event("input", { bubbles: true }));
        } else if (event.key === "ArrowDown")
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
