// A plant's story is printed from the page itself. This waits until every photo on it has loaded,
// so none is missing from the print, and then opens the browser's print dialog.

const WAIT_MS = 20000;

// Resolves to how many photos could not be shown. The print hides those instead of leaving a hole.
export async function print() {
    const started = performance.now();

    // PhotoImage marks a photo as pending while it reads it, or fetches the full size from the server
    while (document.querySelector(".story-paper [data-pending]") && performance.now() - started < WAIT_MS) {
        await new Promise(resolve => setTimeout(resolve, 100));
    }

    const images = [...document.querySelectorAll(".story-paper img")];
    const left = Math.max(0, WAIT_MS - (performance.now() - started));
    await Promise.race([
        Promise.all(images.map(image => image.decode().catch(() => { }))),
        new Promise(resolve => setTimeout(resolve, left))
    ]);

    const missing = document.querySelectorAll(".story-paper .photo-placeholder").length;
    window.print();
    return missing;
}
