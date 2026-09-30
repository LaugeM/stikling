// A bottom sheet is a native <dialog>, so focus, Escape and the page behind it being
// inert all come from the browser. Blazor only says when it should be open.

export function open(dialog, owner) {
    if (!dialog || dialog.open)
        return;

    if (!dialog.dataset.wired) {
        dialog.dataset.wired = "true";
        // Escape, the back gesture on Android, or a tap on the dimmed page
        dialog.addEventListener("close", () => dialog.stiklingOwner?.invokeMethodAsync("Closed"));
        dialog.addEventListener("click", event => {
            if (event.target === dialog)
                dialog.close();
        });
    }

    dialog.stiklingOwner = owner;
    dialog.showModal();
}

export function close(dialog) {
    if (dialog?.open)
        dialog.close();
}
