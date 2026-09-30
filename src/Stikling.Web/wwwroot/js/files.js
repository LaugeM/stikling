// Saving a file to the phone or computer, and reading small values that belong to this
// device only (when the last backup was taken, whether someone is signed in here).

export function download(fileName, bytes, type = "application/zip") {
    save(fileName, new Blob([bytes], { type }));
}

// Asks the API for a file and saves it, so a large one never passes through .NET. The status the
// API answered with, or -1 when it couldn't be reached.
export async function downloadFrom(address, token, body, fileName) {
    let blob;
    try {
        const response = await fetch(address, {
            method: "POST",
            headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
            body: JSON.stringify(body),
        });
        if (!response.ok) return response.status;
        blob = await response.blob();
    } catch {
        return -1;
    }

    save(fileName, blob);
    return 200;
}

function save(fileName, blob) {
    const url = URL.createObjectURL(blob);
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
