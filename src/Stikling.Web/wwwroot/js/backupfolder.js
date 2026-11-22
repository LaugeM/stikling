// The folder backups are saved to, on browsers that can write into a folder (Chrome and Edge, and Chrome on
// Android from 132). The folder's handle is kept in a small database of its own, apart from the app's data:
// it belongs to this device only, so it isn't synced, isn't in a backup, and needs no change to the main
// database. A browser drops the permission to write when the app is closed and gives it back on a tap, which
// is why requestPermission is called from the tap that starts a backup.

const DATABASE = "stikling-device";
const STORE = "handles";
const KEY = "backup-folder";

export function isSupported() {
    return typeof window.showDirectoryPicker === "function";
}

function open() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DATABASE, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function run(mode, action) {
    const db = await open();
    try {
        return await new Promise((resolve, reject) => {
            const transaction = db.transaction(STORE, mode);
            const request = action(transaction.objectStore(STORE));
            transaction.oncomplete = () => resolve(request.result);
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });
    } finally {
        db.close();
    }
}

async function handle() {
    if (!isSupported()) return null;
    try {
        return (await run("readonly", store => store.get(KEY))) ?? null;
    } catch {
        return null;
    }
}

// The name of the folder, or null when none is set
export async function getName() {
    return (await handle())?.name ?? null;
}

// Lets the person pick a folder and keeps it. Returns its name, or null when they cancelled.
export async function choose() {
    let picked;
    try {
        picked = await window.showDirectoryPicker({ id: "stikling-backups", mode: "readwrite" });
    } catch {
        return null; // closed without choosing, or not allowed
    }
    await run("readwrite", store => store.put(picked, KEY));
    return picked.name;
}

export async function forget() {
    try {
        await run("readwrite", store => store.delete(KEY));
    } catch {
        // Nothing was kept
    }
}

// Asks for permission to write to the folder. It has to be called straight from a tap, before anything slow,
// since the tap only counts for a few seconds. False when there is no folder or the answer was no.
export async function requestPermission() {
    const folder = await handle();
    if (!folder) return false;
    try {
        const options = { mode: "readwrite" };
        if ((await folder.queryPermission(options)) === "granted") return true;
        return (await folder.requestPermission(options)) === "granted";
    } catch {
        return false;
    }
}

// Writes the file into the folder under a name that isn't taken, adding (2), (3) and so on, so nothing there is
// ever replaced. Returns the name it got, or null when it couldn't be written, e.g. the folder is gone.
export async function write(fileName, bytes) {
    const folder = await handle();
    if (!folder) return null;
    try {
        const dot = fileName.lastIndexOf(".");
        const stem = dot > 0 ? fileName.slice(0, dot) : fileName;
        const extension = dot > 0 ? fileName.slice(dot) : "";

        let name = fileName;
        for (let n = 2; await exists(folder, name); n++) {
            name = `${stem} (${n})${extension}`;
        }

        const file = await folder.getFileHandle(name, { create: true });
        const writable = await file.createWritable();
        try {
            await writable.write(bytes);
        } finally {
            await writable.close();
        }
        return name;
    } catch {
        return null;
    }
}

async function exists(folder, name) {
    try {
        await folder.getFileHandle(name);
        return true;
    } catch (error) {
        if (error?.name === "NotFoundError") return false;
        throw error;
    }
}
