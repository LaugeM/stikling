// Thin wrapper around IndexedDB, the browser's built-in database.
// Imported as an ES module from C# (see Services/IndexedDb.cs). Values are plain
// JSON objects with an "id" property as the key.

const DB_NAME = "stikling";
const DB_VERSION = 6;

// The stores the first version created. Later versions add theirs in their own
// upgrade block below, so don't add to this list.
const STORES_V1 = ["plants", "propagations", "timeline", "photos", "photoBlobs"];

let dbPromise;

function openDb() {
    dbPromise ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        // Runs when the database is created or DB_VERSION goes up. Each version's changes
        // go in their own "if (event.oldVersion < N)" block, and the blocks run in order:
        // a new device runs all of them, an existing one only the blocks it hasn't yet.
        // Never change an old block, or devices that already ran it won't get the fix.
        request.onupgradeneeded = event => {
            const db = request.result;
            if (event.oldVersion < 1) {
                for (const name of STORES_V1) {
                    if (name === "photoBlobs") db.createObjectStore(name); // key given on put
                    else db.createObjectStore(name, { keyPath: "id" });
                }
            }
            if (event.oldVersion < 2) {
                db.createObjectStore("careLogs", { keyPath: "id" });
            }
            if (event.oldVersion < 3) {
                db.createObjectStore("pestCases", { keyPath: "id" });
                db.createObjectStore("pestTreatments", { keyPath: "id" });
            }
            if (event.oldVersion < 4) {
                db.createObjectStore("pots", { keyPath: "id" });
            }
            if (event.oldVersion < 5) {
                db.createObjectStore("soilMixes", { keyPath: "id" });
            }
            if (event.oldVersion < 6) {
                db.createObjectStore("products", { keyPath: "id" });
            }
        };

        request.onsuccess = () => {
            const db = request.result;
            // Another tab upgraded the database: close so it isn't blocked
            db.onversionchange = () => db.close();
            resolve(db);
        };
        request.onerror = () => {
            dbPromise = undefined;
            reject(request.error);
        };
    });
    return dbPromise;
}

// Runs one request in its own transaction and resolves with its result
async function run(storeName, mode, action) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, mode);
        const request = action(tx.objectStore(storeName));
        tx.oncomplete = () => resolve(request.result);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
    });
}

export function getAll(storeName) {
    return run(storeName, "readonly", store => store.getAll());
}

export async function get(storeName, key) {
    // IndexedDB returns undefined for a missing key; null maps cleanly to C# null
    return (await run(storeName, "readonly", store => store.get(key))) ?? null;
}

export async function put(storeName, value) {
    await run(storeName, "readwrite", store => store.put(value));
}

export async function remove(storeName, key) {
    await run(storeName, "readwrite", store => store.delete(key));
}

// Binary data (photos) lives in its own store with the key given explicitly
export async function putBlob(key, blob) {
    await run("photoBlobs", "readwrite", store => store.put(blob, key));
}

export async function getBlob(key) {
    return (await run("photoBlobs", "readonly", store => store.get(key))) ?? null;
}

export async function removeBlob(key) {
    await run("photoBlobs", "readwrite", store => store.delete(key));
}

// Asks the browser not to clear our data when the device is low on space.
// Installed PWAs usually get this automatically. Returns true when granted.
export async function requestPersistence() {
    if (!navigator.storage?.persist) return false;
    if (await navigator.storage.persisted()) return true;
    return navigator.storage.persist();
}

// Storage use in bytes, for the Settings page
export async function estimate() {
    if (!navigator.storage?.estimate) return null;
    const { usage, quota } = await navigator.storage.estimate();
    return { usage: usage ?? 0, quota: quota ?? 0, persisted: (await navigator.storage.persisted?.()) ?? false };
}
