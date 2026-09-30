// A bottom sheet is a native <dialog>, so focus, Escape and the page behind it being
// inert all come from the browser. Blazor only says when it should be open.

export function open(dialog, owner, generation) {
    if (!dialog || dialog.open)
        return;

    if (!dialog.dataset.wired) {
        dialog.dataset.wired = "true";
        // Escape, the back gesture on Android, or a tap on the dimmed page. The event can come
        // late, so it's dropped when the sheet has opened again since, and it says which
        // opening it closed so Blazor can tell too.
        dialog.addEventListener("close", () => {
            if (!dialog.open)
                dialog.stiklingOwner?.invokeMethodAsync("Closed", dialog.stiklingGeneration);
        });
        dialog.addEventListener("click", event => {
            if (event.target === dialog)
                dialog.close();
        });
    }

    dialog.stiklingOwner = owner;
    dialog.stiklingGeneration = generation;
    dialog.showModal();
}

// The page is going away: close without telling Blazor, which has let go of the sheet
export function release(dialog) {
    if (!dialog)
        return;
    dialog.stiklingOwner = null;
    close(dialog);
}

export function close(dialog) {
    if (dialog?.open)
        dialog.close();
}
