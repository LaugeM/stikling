// Saving a file to the phone or computer, and reading small values that belong to this
// device only (which theme, when the last backup was taken).

export function download(fileName, bytes, type = "application/zip") {
    const url = URL.createObjectURL(new Blob([bytes], { type }));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    // Give the browser a moment to start the download before dropping the data
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

const prefix = "stikling-";

export function get(key) {
    try {
        return localStorage.getItem(prefix + key);
    } catch {
        return null; // private mode or blocked storage
    }
}

export function set(key, value) {
    try {
        if (value === null) localStorage.removeItem(prefix + key);
        else localStorage.setItem(prefix + key, value);
    } catch {
        // Not being able to remember a setting shouldn't break anything
    }
}
