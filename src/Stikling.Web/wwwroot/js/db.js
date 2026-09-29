// Thin wrapper around IndexedDB, the browser's built-in database.
// Imported as an ES module from C# (see Services/IndexedDb.cs). Values are plain
// JSON objects with an "id" property as the key.

const DB_NAME = "stikling";
const DB_VERSION = 11;

// The stores the first version created. Later versions add theirs in their own
// upgrade block below, so don't add to this list.
const STORES_V1 = ["plants", "propagations", "timeline", "photos", "photoBlobs"];

// Stores that don't hold records. Every other store is synced, so a write to it also goes on
// the change list, the "changes" store, in the same step.
const NOT_RECORDS = new Set(["photoBlobs", "changes", "syncState"]);

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
            if (event.oldVersion < 7) {
                db.createObjectStore("feeds", { keyPath: "id" });
            }
            if (event.oldVersion < 8) {
                db.createObjectStore("treatmentRecipes", { keyPath: "id" });
            }
            if (event.oldVersion < 9) {
                db.createObjectStore("places", { keyPath: "id" });
            }
            if (event.oldVersion < 10) {
                db.createObjectStore("settings", { keyPath: "id" });
                db.createObjectStore("putOffs", { keyPath: "id" });
            }
            if (event.oldVersion < 11) {
                // Records changed here that the server hasn't had yet, keyed "kind/id"
                db.createObjectStore("changes", { keyPath: "key" });
                // How far this device has got with syncing, under the id "state"
                db.createObjectStore("syncState", { keyPath: "id" });
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

// Runs work in one transaction over the stores, and resolves once it has committed. The work
// returns a function that gives the result, since requests only have theirs by then.
async function transact(storeNames, mode, work) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeNames, mode);
        const result = work(tx);
        tx.oncomplete = () => resolve(result?.());
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
    });
}

// Runs one request in its own transaction and resolves with its result
function run(storeName, mode, action) {
    return transact(storeName, mode, tx => {
        const request = action(tx.objectStore(storeName));
        return () => request.result;
    });
}

// Puts a record on the change list. The mark is new on every change, so a record changed again
// while a sync is sending it stays on the list.
function noteChange(tx, kind, id) {
    tx.objectStore("changes").put({ key: `${kind}/${id}`, kind, id, mark: crypto.randomUUID() });
}

export function getAll(storeName) {
    return run(storeName, "readonly", store => store.getAll());
}

export async function get(storeName, key) {
    // IndexedDB returns undefined for a missing key; null maps cleanly to C# null
    return (await run(storeName, "readonly", store => store.get(key))) ?? null;
}

export async function put(storeName, value) {
    if (NOT_RECORDS.has(storeName)) {
        await run(storeName, "readwrite", store => store.put(value));
        return;
    }

    await transact([storeName, "changes"], "readwrite", tx => {
        const store = tx.objectStore(storeName);
        const request = store.get(value.id);
        request.onsuccess = () => {
            // Fields this version of the app doesn't know are kept, so a device that hasn't updated
            // yet doesn't erase what a newer version added on another device. The fields it knows
            // are always written, even when empty, so this never brings back a cleared value.
            const stored = request.result;
            if (stored)
                for (const [name, field] of Object.entries(stored))
                    if (!(name in value)) value[name] = field;

            store.put(value);
            noteChange(tx, storeName, value.id);
        };
    });
}

export async function remove(storeName, key) {
    if (NOT_RECORDS.has(storeName)) {
        await run(storeName, "readwrite", store => store.delete(key));
        return;
    }

    await transact([storeName, "changes"], "readwrite", tx => {
        tx.objectStore(storeName).delete(key);
        noteChange(tx, storeName, key);
    });
}

// Binary data (photos) lives in its own store with the key given explicitly
export async function putBlob(key, blob) {
    await run("photoBlobs", "readwrite", store => store.put(blob, key));
}

export async function getBlob(key) {
    return (await run("photoBlobs", "readonly", store => store.get(key))) ?? null;
}

export async function hasBlob(key) {
    return (await run("photoBlobs", "readonly", store => store.count(key))) > 0;
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

// What sync needs from the device. IndexedDbSyncStore in C# is the only caller.

export async function getSyncState() {
    return (await run("syncState", "readonly", store => store.get("state"))) ?? null;
}

export async function saveSyncState(state) {
    await run("syncState", "readwrite", store => store.put({ ...state, id: "state" }));
}

export async function forgetSyncState() {
    await run("syncState", "readwrite", store => store.delete("state"));
}

// Up to max entries from the change list, of the given kinds, each with the record as it is now,
// or null when it is no longer here
export function getPending(kinds, max) {
    const wanted = new Set(kinds);
    return transact(["changes", ...wanted], "readonly", tx => {
        const found = [];
        const cursorRequest = tx.objectStore("changes").openCursor();
        cursorRequest.onsuccess = () => {
            const cursor = cursorRequest.result;
            if (!cursor || found.length >= max) return;

            const { kind, id, mark } = cursor.value;
            if (wanted.has(kind)) {
                const pending = { kind, id, data: null, mark };
                found.push(pending);
                const request = tx.objectStore(kind).get(id);
                request.onsuccess = () => pending.data = request.result ?? null;
            }
            cursor.continue();
        };
        return () => found;
    });
}

export async function countPending() {
    return await run("changes", "readonly", store => store.count());
}

// Takes sent records off the change list, unless they changed again since they were read
export async function markSent(sent) {
    await transact("changes", "readwrite", tx => {
        const changes = tx.objectStore("changes");
        for (const { kind, id, mark } of sent) {
            const key = `${kind}/${id}`;
            const request = changes.get(key);
            request.onsuccess = () => {
                if (request.result?.mark === mark) changes.delete(key);
            };
        }
    });
}

// Puts every record of these kinds on the change list
export async function queueAll(kinds) {
    await transact(["changes", ...kinds], "readwrite", tx => {
        for (const kind of kinds) {
            const request = tx.objectStore(kind).getAllKeys();
            request.onsuccess = () => {
                for (const id of request.result) noteChange(tx, kind, id);
            };
        }
    });
}

export function getMany(kind, ids) {
    return transact(kind, "readonly", tx => {
        const found = [];
        for (const id of ids) {
            const request = tx.objectStore(kind).get(id);
            request.onsuccess = () => {
                if (request.result) found.push(request.result);
            };
        }
        return () => found;
    });
}

// Saves records from the server, each only if the copy here still has the updatedAt it was
// compared with (or is still missing, when replaces is null). They don't go on the change list,
// and a change here that lost to them comes off it. A photo deleted on another device frees its
// image here too (under the keys photos.js keeps it by).
export async function saveFromServer(kind, copies) {
    const stores = kind === "photos" ? [kind, "changes", "photoBlobs"] : [kind, "changes"];
    await transact(stores, "readwrite", tx => {
        const store = tx.objectStore(kind);
        for (const { data, replaces } of copies) {
            const request = store.get(data.id);
            request.onsuccess = () => {
                if ((request.result?.updatedAt ?? null) !== replaces) return;

                store.put(data);
                tx.objectStore("changes").delete(`${kind}/${data.id}`);
                if (kind === "photos" && data.deletedAt) {
                    tx.objectStore("photoBlobs").delete(data.id);
                    tx.objectStore("photoBlobs").delete(`${data.id}:thumb`);
                }
            };
        }
    });
}

// How many photos have their image on this device
export async function countPhotoImages() {
    const keys = await run("photoBlobs", "readonly", store => store.getAllKeys());
    return keys.filter(key => !String(key).endsWith(":thumb")).length;
}

// Empties every store, for signing out and removing the data from this device
export async function clearAll() {
    const db = await openDb();
    const names = Array.from(db.objectStoreNames);
    await transact(names, "readwrite", tx => {
        for (const name of names) tx.objectStore(name).clear();
    });
}
