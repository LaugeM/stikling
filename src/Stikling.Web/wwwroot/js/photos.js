// Photo handling that stays in the browser: resizing, compressing (WebP, or JPEG where the
// browser can't make WebP), storing, displaying,
// and sending to and fetching from the sync API. Full-size camera photos (often 5-10 MB)
// never cross into .NET.

import { getBlob, hasBlob, putPhotoImages, putFetchedImage, removePhotoImages } from "./db.js";

const FULL_SIZE = 1600;   // longest side in pixels
const THUMB_SIZE = 360;
const WEBP_QUALITY = 0.8;  // WebP quality: about the same look as JPEG at 0.82, in a smaller file
const JPEG_QUALITY = 0.82; // JPEG quality, for browsers that can't make WebP: small files, no visible loss on a phone

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

    const toBlob = (type, quality) => new Promise(resolve => canvas.toBlob(resolve, type, quality));

    // A browser that can't make WebP quietly gives back a PNG (or null), so check what came back
    let blob = await toBlob("image/webp", WEBP_QUALITY);
    if (blob?.type !== "image/webp")
        blob = await toBlob("image/jpeg", JPEG_QUALITY);
    return { blob, width, height };
}

// The type of an image from how its bytes start: WebP is "RIFF", four size bytes, then "WEBP".
// Anything else is taken to be a JPEG, which is what photos saved before WebP are.
function imageType(bytes) {
    const at = (offset, text) => [...text].every((c, i) => bytes[offset + i] === c.charCodeAt(0));
    return bytes.length >= 12 && at(0, "RIFF") && at(8, "WEBP") ? "image/webp" : "image/jpeg";
}

// The date the camera saved in a JPEG, as the text it wrote, e.g. "2023:05:14 10:22:31".
// That is EXIF DateTimeOriginal, or the plain DateTime when a phone only wrote that one.
// Null for other formats and for photos without a date, e.g. ones sent through a chat app.
async function readCameraDate(file) {
    try {
        // EXIF sits before the image data, well inside the first 256 KB
        const view = new DataView(await file.slice(0, 256 * 1024).arrayBuffer());
        if (view.getUint16(0) !== 0xFFD8) return null;

        let offset = 2;
        while (offset + 10 <= view.byteLength) {
            const marker = view.getUint16(offset);
            if ((marker & 0xFF00) !== 0xFF00 || marker === 0xFFDA) return null;

            const isExif = marker === 0xFFE1 && view.getUint32(offset + 4) === 0x45786966; // "Exif"
            if (isExif) return readExifDate(view, offset + 10);

            offset += 2 + view.getUint16(offset + 2);
        }
    } catch {
        // A damaged or cut-off header reads past the end; treat it as having no date
    }
    return null;
}

function readExifDate(view, tiff) {
    const little = view.getUint16(tiff) === 0x4949; // "II"
    const u16 = at => view.getUint16(at, little);
    const u32 = at => view.getUint32(at, little);

    const tags = at => {
        const found = new Map();
        const count = u16(at);
        for (let i = 0; i < count; i++) {
            const entry = at + 2 + i * 12;
            found.set(u16(entry), entry);
        }
        return found;
    };

    const text = entry => {
        const length = u32(entry + 4);
        const at = length > 4 ? tiff + u32(entry + 8) : entry + 8;
        let value = "";
        for (let i = 0; i < length; i++) {
            const c = view.getUint8(at + i);
            if (c === 0) break;
            value += String.fromCharCode(c);
        }
        return value || null;
    };

    const main = tags(tiff + u32(tiff + 4));
    const exifPointer = main.get(0x8769);
    if (exifPointer !== undefined) {
        const original = tags(tiff + u32(exifPointer + 8)).get(0x9003);
        if (original !== undefined) return text(original);
    }

    const plain = main.get(0x0132);
    return plain !== undefined ? text(plain) : null;
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
            await putPhotoImages(id, full.blob, thumb.blob);

            // The date is picked in .NET from these three
            saved.push({
                id,
                width: full.width,
                height: full.height,
                cameraDate: await readCameraDate(file),
                fileName: file.name,
                fileDate: new Date(file.lastModified || Date.now()).toISOString()
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
    forgetUrls(id);
    await removePhotoImages(id);
}

function forgetUrls(id) {
    for (const key of [id, thumbKey(id)]) {
        const url = urlCache.get(key);
        if (url) {
            URL.revokeObjectURL(url);
            urlCache.delete(key);
        }
    }
}

// Backup and restore work on raw bytes: the photo itself and its thumbnail.
export async function getBytes(id, thumbnail) {
    const blob = await getBlob(thumbnail ? thumbKey(id) : id);
    return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
}

export function hasBytes(id) {
    return hasBlob(id);
}

export async function putBytes(id, bytes, thumbBytes) {
    const thumb = thumbBytes ? new Blob([thumbBytes], { type: imageType(thumbBytes) }) : null;
    await putPhotoImages(id, new Blob([bytes], { type: imageType(bytes) }), thumb);

    // Drop any object URL made before the photo came back
    forgetUrls(id);
}

// Sync. SyncRunner and StiklingApi give the address and a fresh sign-in token, and the image
// goes straight between the device database and the API. Each returns the HTTP status, 0 when
// the image isn't on this device, or -1 when the API couldn't be reached.

export async function upload(address, token, id, thumbnail) {
    const blob = await getBlob(thumbnail ? thumbKey(id) : id);
    if (!blob) return 0;

    try {
        const response = await fetch(address, {
            method: "PUT",
            headers: { "Authorization": `Bearer ${token}`, "Content-Type": imageType(new Uint8Array(await blob.slice(0, 12).arrayBuffer())) },
            body: blob
        });
        return response.status;
    } catch {
        return -1;
    }
}

// Keeps the image once it has arrived, and doesn't put it on the list to send back
export async function download(address, token, id, thumbnail) {
    let blob;
    try {
        const response = await fetch(address, { headers: { "Authorization": `Bearer ${token}` } });
        if (!response.ok) return response.status;
        const bytes = new Uint8Array(await response.arrayBuffer());
        blob = new Blob([bytes], { type: imageType(bytes) });
    } catch {
        return -1;
    }

    await putFetchedImage(id, thumbnail ? thumbKey(id) : id, blob);
    forgetUrls(id);
    return 200;
}
