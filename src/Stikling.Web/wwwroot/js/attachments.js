// Files attached to a plant, a propagation or an order: receipts, plant tags, care cards, as a PDF or an
// image. Like photos, the bytes stay in the browser. They are prepared, stored in fileBlobs, opened,
// saved, backed up and sent to and fetched from the sync API here, and only the details reach .NET.

import { getFileBlob, hasFileBlob, putFileBytes, putFetchedFile, removeFileBytes } from "./db.js";
import { resize } from "./photos.js";
import { download as saveToDevice } from "./files.js";

const MAX_BYTES = 10 * 1024 * 1024; // the same as FileRules.MaxFileBytes
const MAX_NAME = 200;               // the same as AttachedFile.MaxNameLength
const LONG_SIDE = 2400;             // images are shrunk to this many pixels on the long side, never enlarged
const QUALITY = 0.85;               // WebP, and JPEG in a browser that can't make WebP

const urlCache = new Map();

const startsWith = (bytes, text) => [...text].every((c, i) => bytes[i] === c.charCodeAt(0));

// The type of stored bytes: a PDF starts with "%PDF-", WebP is "RIFF", four size bytes, then "WEBP", and
// anything else is a JPEG
function typeOf(bytes) {
    if (startsWith(bytes, "%PDF-")) return "application/pdf";
    const webp = bytes.length >= 12 && startsWith(bytes, "RIFF") && [..."WEBP"].every((c, i) => bytes[8 + i] === c.charCodeAt(0));
    return webp ? "image/webp" : "image/jpeg";
}

const extensionOf = type => type === "application/pdf" ? "pdf" : type === "image/webp" ? "webp" : "jpg";

async function isPdf(file) {
    return startsWith(new Uint8Array(await file.slice(0, 5).arrayBuffer()), "%PDF-");
}

function cleanName(name) {
    const trimmed = (name ?? "").trim().slice(0, MAX_NAME).trim();
    return trimmed || "File";
}

// Checks one chosen file and keeps its bytes, ready to send to the server. A PDF is kept exactly as it is.
// An image is shrunk and saved as WebP (JPEG where the browser can't), whatever it was. Resolves with
// { ok: true, id, name, contentType, size }, or { ok: false, error } where error is "unsupported" for
// something that is neither a PDF nor an image the browser can read, and "tooLarge" for more than 10 MB
// (of the PDF, or of the image after shrinking). The caller writes the record with the same id, or
// calls discard(id) if it doesn't.
export async function prepare(file) {
    try {
        let blob;
        if (await isPdf(file)) {
            if (file.size > MAX_BYTES) return { ok: false, error: "tooLarge" };
            blob = file.slice(0, file.size, "application/pdf");
        } else {
            let bitmap;
            try {
                // "from-image" applies the camera's rotation info so pictures aren't sideways
                bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
            } catch {
                return { ok: false, error: "unsupported" };
            }

            try {
                blob = (await resize(bitmap, LONG_SIDE, QUALITY, QUALITY)).blob;
            } finally {
                bitmap.close();
            }
            if (!blob) return { ok: false, error: "unsupported" };
            if (blob.size > MAX_BYTES) return { ok: false, error: "tooLarge" };
        }

        const id = crypto.randomUUID();
        await putFileBytes(id, blob);
        return { ok: true, id, name: cleanName(file.name), contentType: blob.type, size: blob.size };
    } catch {
        return { ok: false, error: "unsupported" };
    }
}

// Prepares the files chosen in an <input type="file">, at most max of them. The others are counted in
// "skipped". The errors are the codes of prepare, one for each file that couldn't be kept.
export async function saveFromInput(input, max) {
    const saved = [];
    const errors = [];
    const files = Array.from(input.files ?? []);

    for (const file of files.slice(0, Math.max(0, max))) {
        const result = await prepare(file);
        if (result.ok) saved.push(result);
        else errors.push(result.error);
    }

    input.value = ""; // allow choosing the same file again
    return { saved, errors, skipped: Math.max(0, files.length - Math.max(0, max)) };
}

// Frees the bytes of a file that was prepared but never got a record
export async function discard(id) {
    forgetUrl(id);
    await removeFileBytes(id);
}

export function has(id) {
    return hasFileBlob(id);
}

async function urlFor(id) {
    if (urlCache.has(id)) return urlCache.get(id);

    const blob = await getFileBlob(id);
    if (!blob) return null;

    const url = URL.createObjectURL(blob);
    urlCache.set(id, url);
    return url;
}

// Opens the file in a new tab, where the browser shows a PDF or an image. The tab is opened first, while
// the tap that asked for it still counts, so a pop-up blocker lets it through. False when the bytes aren't
// on this device.
export async function open(id) {
    const tab = window.open("", "_blank");
    const url = await urlFor(id);
    if (!url) {
        tab?.close();
        return false;
    }

    if (tab) {
        tab.location.href = url;
        return true;
    }

    // Pop-ups are blocked: go through a link instead
    const link = document.createElement("a");
    link.href = url;
    link.target = "_blank";
    link.rel = "noopener";
    document.body.appendChild(link);
    link.click();
    link.remove();
    return true;
}

// The name the file is saved under: the one given, with the extension the bytes really have
function downloadName(name, type) {
    const extension = extensionOf(type);
    const base = type === "application/pdf"
        ? name
        : name.replace(/\.(jpe?g|png|webp|gif|heic|heif|avif|bmp|tiff?)$/i, "");
    return base.toLowerCase().endsWith(`.${extension}`) ? base : `${base}.${extension}`;
}

// Saves the file to the device. False when the bytes aren't on this device.
export async function save(id, name) {
    const blob = await getFileBlob(id);
    if (!blob) return false;

    saveToDevice(downloadName(cleanName(name), blob.type), blob, blob.type);
    return true;
}

function forgetUrl(id) {
    const url = urlCache.get(id);
    if (url) {
        URL.revokeObjectURL(url);
        urlCache.delete(id);
    }
}

// Backup and restore work on raw bytes
export async function getBytes(id) {
    const blob = await getFileBlob(id);
    return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
}

export async function putBytes(id, bytes) {
    await putFileBytes(id, new Blob([bytes], { type: typeOf(bytes) }));
    forgetUrl(id);
}

// Frees the bytes of a file whose record is deleted. A record written deleted frees them in db.js too,
// so this only drops the object URL and covers bytes that are left over.
export async function remove(id) {
    forgetUrl(id);
    await removeFileBytes(id);
}

// Sync. SyncRunner and StiklingApi give the address and a fresh sign-in token, and the bytes go straight
// between the device database and the API. Each returns the HTTP status, 0 when the bytes aren't on this
// device, or -1 when the API couldn't be reached.

export async function upload(address, token, id) {
    const blob = await getFileBlob(id);
    if (!blob) return 0;

    try {
        const response = await fetch(address, {
            method: "PUT",
            headers: { "Authorization": `Bearer ${token}`, "Content-Type": blob.type || "application/octet-stream" },
            body: blob
        });
        return response.status;
    } catch {
        return -1;
    }
}

// Keeps the bytes once they have arrived, and doesn't put the file on the list to send back
export async function download(address, token, id) {
    let blob;
    try {
        const response = await fetch(address, { headers: { "Authorization": `Bearer ${token}` } });
        if (!response.ok) return response.status;
        const bytes = new Uint8Array(await response.arrayBuffer());
        blob = new Blob([bytes], { type: typeOf(bytes) });
    } catch {
        return -1;
    }

    await putFetchedFile(id, blob);
    forgetUrl(id);
    return 200;
}
