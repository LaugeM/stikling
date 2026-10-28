// The plant-sitter sheet is printed from the page itself, so this only has to start the print
// dialog (after the photos on the sheet have loaded) and share or copy the sheet as text.

// Waits for the images on the sheet, so none is missing from the print
export async function print() {
    const images = [...document.querySelectorAll(".sitter-paper img")];
    await Promise.all(images.map(image => image.complete
        ? Promise.resolve()
        : new Promise(resolve => {
            image.addEventListener("load", resolve, { once: true });
            image.addEventListener("error", resolve, { once: true });
        })));
    window.print();
}

// "shared", "cancelled" (closed the share menu), "copied" or "failed"
export async function shareText(text) {
    if (typeof navigator.share === "function") {
        try {
            await navigator.share({ text });
            return "shared";
        } catch (error) {
            if (error?.name === "AbortError")
                return "cancelled";
        }
    }
    return (await copy(text)) ? "copied" : "failed";
}

async function copy(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        // A browser that won't give the page the clipboard directly
        const field = document.createElement("textarea");
        field.value = text;
        field.setAttribute("readonly", "");
        field.style.position = "fixed";
        field.style.opacity = "0";
        document.body.appendChild(field);
        field.select();
        try {
            return document.execCommand("copy");
        } catch {
            return false;
        } finally {
            field.remove();
        }
    }
}
