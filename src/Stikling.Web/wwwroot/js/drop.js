// Dropping photo files from the computer onto a plant, a propagation or a row in a list. The files go to
// the same hidden file input that the Photo button uses, so the same code opens the sheet and saves them.

const isFileDrag = event => !!event.dataTransfer && [...event.dataTransfer.types].includes("Files");

const imagesIn = event => [...event.dataTransfer.files].filter(file => file.type.startsWith("image/"));

// Hands the files to the input as if they had been picked. A busy composer has its input disabled.
function give(input, files) {
    if (!input || input.disabled || files.length === 0)
        return false;
    const transfer = new DataTransfer();
    files.forEach(file => transfer.items.add(file));
    input.files = transfer.files;
    input.dispatchEvent(new Event("change", { bubbles: true }));
    return true;
}

// A file dropped on a part of the page that doesn't take it would make the browser open it and leave the app
function keepInPage() {
    const stop = event => {
        if (isFileDrag(event))
            event.preventDefault();
    };
    document.addEventListener("dragover", stop);
    document.addEventListener("drop", stop);
    return () => {
        document.removeEventListener("dragover", stop);
        document.removeEventListener("drop", stop);
    };
}

// The whole page takes the files for one subject. Returns something to call dispose() on.
export function attachPage(inputId, name) {
    const overlay = document.createElement("div");
    overlay.className = "drop-overlay";
    overlay.hidden = true;
    const label = document.createElement("span");
    label.textContent = `Drop to add to ${name}`;
    overlay.append(label);
    document.body.append(overlay);

    let depth = 0;
    const hide = () => {
        depth = 0;
        overlay.hidden = true;
    };
    const onEnter = event => {
        if (!isFileDrag(event))
            return;
        depth++;
        overlay.hidden = false;
    };
    const onLeave = event => {
        if (!isFileDrag(event))
            return;
        depth = Math.max(0, depth - 1);
        if (depth === 0 || event.relatedTarget === null)
            hide();
    };
    const onDrop = event => {
        if (!isFileDrag(event))
            return;
        hide();
        give(document.getElementById(inputId), imagesIn(event));
    };
    const onKey = event => {
        if (event.key === "Escape")
            hide();
    };

    const release = keepInPage();
    document.addEventListener("dragenter", onEnter);
    document.addEventListener("dragleave", onLeave);
    document.addEventListener("drop", onDrop);
    document.addEventListener("keydown", onKey);
    return {
        dispose: () => {
            release();
            document.removeEventListener("dragenter", onEnter);
            document.removeEventListener("dragleave", onLeave);
            document.removeEventListener("drop", onDrop);
            document.removeEventListener("keydown", onKey);
            overlay.remove();
        }
    };
}

const subjectPattern = /^(plants|propagations)\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

// The row under an element, with what it is for: rows carry data-drop-subject ("plants/{id}"), and a link to
// the plant or propagation is accepted too.
function rowOf(element) {
    const row = element?.closest?.("li, tr");
    if (!row)
        return null;
    const holders = [...row.querySelectorAll("[data-drop-subject], a[href]")];
    for (const holder of holders) {
        const value = holder.getAttribute("data-drop-subject") ?? holder.getAttribute("href");
        const match = subjectPattern.exec(value ?? "");
        if (match)
            return { row, type: match[1] === "plants" ? "Plant" : "Propagation", id: match[2] };
    }
    return null;
}

// Rows in a list take files for their own plant or propagation. dotnet.Choose(type, id) makes the component
// render a composer for that subject, and the files then go to its input. Returns something to call dispose() on.
export function attachRows(dotnet, inputId) {
    let marked = null;
    const mark = row => {
        if (marked === row)
            return;
        marked?.classList.remove("drop-target");
        marked = row;
        marked?.classList.add("drop-target");
    };

    const onOver = event => {
        if (isFileDrag(event))
            mark(rowOf(event.target)?.row ?? null);
    };
    const onLeave = event => {
        if (marked && event.relatedTarget === null)
            mark(null);
    };
    const onKey = event => {
        if (event.key === "Escape")
            mark(null);
    };
    const onDrop = async event => {
        if (!isFileDrag(event))
            return;
        const found = rowOf(event.target);
        const files = imagesIn(event);
        mark(null);
        if (!found || files.length === 0)
            return;
        await dotnet.invokeMethodAsync("Choose", found.type, found.id);
        // The composer renders its input with the render the call waited for, but give the browser a frame if not
        for (let tries = 0; tries < 10 && !document.getElementById(inputId); tries++)
            await new Promise(resolve => requestAnimationFrame(resolve));
        give(document.getElementById(inputId), files);
    };

    const release = keepInPage();
    document.addEventListener("dragover", onOver);
    document.addEventListener("dragleave", onLeave);
    document.addEventListener("drop", onDrop);
    document.addEventListener("keydown", onKey);
    return {
        dispose: () => {
            release();
            document.removeEventListener("dragover", onOver);
            document.removeEventListener("dragleave", onLeave);
            document.removeEventListener("drop", onDrop);
            document.removeEventListener("keydown", onKey);
            marked?.classList.remove("drop-target");
        }
    };
}
