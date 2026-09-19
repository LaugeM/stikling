// Photo handling that stays in the browser: resizing, compressing, storing and
// displaying. Full-size camera photos (often 5-10 MB) never cross into .NET.

import { putBlob, getBlob, removeBlob } from "./db.js";

const FULL_SIZE = 1600;   // longest side in pixels
const THUMB_SIZE = 360;
const QUALITY = 0.82;     // JPEG quality: small files, no visible loss on a phone

const urlCache = new Map();

const thumbKey = id => `${id}:thumb`;

async function resize(bitmap, maxSide) {
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    const width = Math.round(bitmap.width * scale);
    const height = Math.round(bitmap.height * scale);

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    ctx.imageSmoothingQuality = "high";
    ctx.drawImage(bitmap, 0, 0, width, height);

    const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", QUALITY));
    return { blob, width, height };
}

// Reads the files chosen in an <input type="file">, stores a compressed copy and a
// thumbnail of each, and returns their details. Files that aren't readable images
// are skipped and counted in "failed".
export async function saveFromInput(input) {
    const saved = [];
    let failed = 0;

    for (const file of Array.from(input.files ?? [])) {
        try {
            // "from-image" applies the camera's rotation info so photos aren't sideways
            const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
            const full = await resize(bitmap, FULL_SIZE);
            const thumb = await resize(bitmap, THUMB_SIZE);
            bitmap.close();

            const id = crypto.randomUUID();
            await putBlob(id, full.blob);
            await putBlob(thumbKey(id), thumb.blob);

            saved.push({
                id,
                width: full.width,
                height: full.height,
                // For a fresh camera photo this is "now"; for gallery files it's the file date
                takenAt: new Date(file.lastModified || Date.now()).toISOString()
            });
        } catch {
            failed++;
        }
    }

    input.value = ""; // allow choosing the same file again
    return { saved, failed };
}

// Returns an object URL for <img src>, or null if the photo is missing.
export async function getUrl(id, thumb) {
    const key = thumb ? thumbKey(id) : id;
    if (urlCache.has(key)) return urlCache.get(key);

    const blob = await getBlob(key);
    if (!blob) return null;

    const url = URL.createObjectURL(blob);
    urlCache.set(key, url);
    return url;
}

export async function remove(id) {
    for (const key of [id, thumbKey(id)]) {
        const url = urlCache.get(key);
        if (url) {
            URL.revokeObjectURL(url);
            urlCache.delete(key);
        }
        await removeBlob(key);
    }
}

// Backup and restore work on raw bytes: the photo itself and its thumbnail.
export async function getBytes(id, thumbnail) {
    const blob = await getBlob(thumbnail ? thumbKey(id) : id);
    return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
}

export async function putBytes(id, bytes, thumbBytes) {
    await putBlob(id, new Blob([bytes], { type: "image/jpeg" }));
    if (thumbBytes) await putBlob(thumbKey(id), new Blob([thumbBytes], { type: "image/jpeg" }));

    // Drop any object URL made before the photo came back
    for (const key of [id, thumbKey(id)]) {
        const url = urlCache.get(key);
        if (url) {
            URL.revokeObjectURL(url);
            urlCache.delete(key);
        }
    }
}
