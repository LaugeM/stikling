// Putting the rows of a list in order by dragging a handle. Pointer events, so a finger and a mouse work
// the same. The rows move live while dragging; on drop the list is put back as it was and Blazor is told
// which row went where, so Blazor stays the one that draws the list. Rows with data-fixed stay in place and
// nothing can be dropped above them.

export function attach(list, dotnet) {
    let drag = null;

    const rows = () => [...list.children];

    const onDown = (event) => {
        const handle = event.target.closest?.("[data-handle]");
        if (!handle || drag || (event.pointerType === "mouse" && event.button !== 0)) {
            return;
        }
        const row = handle.closest("[data-row]");
        if (!row || row.parentElement !== list) {
            return;
        }
        event.preventDefault();
        handle.setPointerCapture(event.pointerId);
        drag = { row, handle, pointerId: event.pointerId, from: rows().indexOf(row), next: row.nextSibling };
        row.classList.add("dragging");
    };

    const onMove = (event) => {
        if (!drag || event.pointerId !== drag.pointerId) {
            return;
        }
        const others = rows().filter(r => r !== drag.row);
        const firstFree = others.filter(r => r.hasAttribute("data-fixed")).length;
        let target = others.length;
        for (let i = firstFree; i < others.length; i++) {
            const box = others[i].getBoundingClientRect();
            if (event.clientY < box.top + box.height / 2) {
                target = i;
                break;
            }
        }
        list.insertBefore(drag.row, others[target] ?? null);
    };

    const finish = (event, drop) => {
        if (!drag || event.pointerId !== drag.pointerId) {
            return;
        }
        const { row, handle, from, next } = drag;
        const to = rows().indexOf(row);
        drag = null;
        row.classList.remove("dragging");
        if (handle.hasPointerCapture?.(event.pointerId)) {
            handle.releasePointerCapture(event.pointerId);
        }
        list.insertBefore(row, next);
        if (drop && to !== from) {
            dotnet.invokeMethodAsync("Moved", from, to);
        }
    };

    const onUp = (event) => finish(event, true);
    const onCancel = (event) => finish(event, false);

    list.addEventListener("pointerdown", onDown);
    list.addEventListener("pointermove", onMove);
    list.addEventListener("pointerup", onUp);
    list.addEventListener("pointercancel", onCancel);

    return {
        dispose() {
            list.removeEventListener("pointerdown", onDown);
            list.removeEventListener("pointermove", onMove);
            list.removeEventListener("pointerup", onUp);
            list.removeEventListener("pointercancel", onCancel);
        }
    };
}
