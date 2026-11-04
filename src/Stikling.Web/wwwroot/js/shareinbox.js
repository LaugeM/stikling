// The photos waiting to be sent to a plant or propagation. They come from Android's share menu
// (service worker, see service-worker.published.js) or from the gallery on the Add photos page, and
// stay in Cache Storage on this device until they are sorted. They are not records: they don't sync
// and aren't in backups. The cache name has no "offline-cache-" prefix, so an app update leaves it alone.

import { saveFile } from "./photos.js";

const CACHE_NAME = "stikling-share-inbox";
const prefix = () => `${new URL("./", document.baseURI).href}share-inbox/`;

// The object URLs handed out by list, so they can be revoked
const urls = new Map();

const idOf = request => request.url.slice(prefix().length);

async function open() {
    return await caches.open(CACHE_NAME);
}

async function entries() {
    const cache = await open();
    const keys = (await cache.keys()).filter(request => request.url.startsWith(prefix()));
    return { cache, keys };
}

function revoke(id) {
    const url = urls.get(id);
    if (url) {
        URL.revokeObjectURL(url);
        urls.delete(id);
    }
}

// How many photos are waiting
export async function count() {
    if (typeof caches === "undefined")
        return 0;
    return (await entries()).keys.length;
}

// The waiting photos, oldest first, each with an address to show it by
export async function list() {
    const { cache, keys } = await entries();
    const found = [];

    for (const request of keys) {
        const response = await cache.match(request);
        if (!response)
            continue;

        const id = idOf(request);
        revoke(id);
        const url = URL.createObjectURL(await response.blob());
        urls.set(id, url);
        found.push({ id, url, name: decodeName(response.headers.get("X-File-Name")) });
    }

    return found;
}

function decodeName(value) {
    try {
        return value ? decodeURIComponent(value) : "";
    } catch {
        return value ?? "";
    }
}

// Puts the files chosen in an <input type="file"> in the inbox as they are, so the camera date in
// them survives. Returns how many were stored. Throws when the browser couldn't store them all.
export async function addFromInput(input) {
    const files = Array.from(input.files ?? []).filter(file => file.type.startsWith("image/"));
    input.value = ""; // allow choosing the same file again

    const cache = await open();
    let stored = 0;
    let error = null;

    for (const file of files) {
        try {
            await cache.put(`${prefix()}${crypto.randomUUID()}`, new Response(file, {
                headers: {
                    "Content-Type": file.type,
                    "X-File-Name": encodeURIComponent(file.name),
                    "X-Last-Modified": String(file.lastModified)
                }
            }));
            stored++;
        } catch (e) {
            error = e;
        }
    }

    if (error)
        throw new Error(`Stored ${stored} of ${files.length}: ${error.message ?? error}`);
    return stored;
}

// Stores the given waiting photos as photos (resized, with thumbnails) and returns the same
// { saved, failed } as saveFromInput, plus savedIds: the inbox id of each photo in saved, in the same
// order. Nothing is removed from the inbox, so a photo that failed stays waiting and the caller
// removes the others once it has recorded them.
export async function save(ids) {
    const cache = await open();
    const saved = [];
    const savedIds = [];
    let failed = 0;

    for (const id of ids) {
        const response = await cache.match(`${prefix()}${id}`);
        if (!response) {
            failed++;
            continue;
        }

        const blob = await response.blob();
        const lastModified = Number(response.headers.get("X-Last-Modified"));
        const file = new File([blob], decodeName(response.headers.get("X-File-Name")), {
            type: response.headers.get("Content-Type") || blob.type,
            lastModified: lastModified > 0 ? lastModified : Date.now()
        });

        const details = await saveFile(file);
        if (details) {
            saved.push(details);
            savedIds.push(id);
        } else {
            failed++;
        }
    }

    return { saved, failed, savedIds };
}

// Takes the given photos out of the inbox
export async function remove(ids) {
    const cache = await open();
    for (const id of ids) {
        await cache.delete(`${prefix()}${id}`);
        revoke(id);
    }
}

// Lets go of the addresses made by list, for when the page is left
export function release() {
    for (const id of [...urls.keys()])
        revoke(id);
}
