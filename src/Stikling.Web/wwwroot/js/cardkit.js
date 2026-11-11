// What the card modules (swapcard.js, experimentcard.js, yearcards.js) have in common: the shell
// that draws a card on a canvas, reading its photos, and keeping the JPEGs for the phone's share
// menu or for saving. Each module calls cardKit() for its own kit and keeps only its layout and
// painting. Each kit has its own map of kept pictures, so ids from different modules never meet.

import { getBlob } from "./db.js";
import { SIZES, loadFonts, toBlob, keep } from "./sharecard.js";

export function cardKit() {
    const kept = new Map(); // id to { blob, url }

    // Draws one picture on the canvas. Returns { url, missing, ...extra }: an object URL of the JPEG,
    // how many photos were not on this device, and whatever paint returned. Null when a newer draw on
    // this canvas has taken over before this one finished (each draw bumps the canvas's token and
    // checks it again after every wait).
    // format: the size to use when spec.format is not one of SIZES.
    // photos: the photo references the card needs ({ photoId, ... }), without the empty ones.
    // paint(ctx, size, bitmaps): bitmaps maps photoId to a decoded photo. It may return an object of
    // extra fields for the result.
    async function draw(canvas, spec, { format, photos, paint }) {
        const token = canvas.stiklingToken = (canvas.stiklingToken ?? 0) + 1;
        const size = SIZES[spec.format] ?? SIZES[format];

        await loadFonts();
        const bitmaps = await readPhotos(photos);
        if (canvas.stiklingToken !== token) {
            closeAll(bitmaps);
            return null;
        }

        canvas.width = size.width;
        canvas.height = size.height;
        const ctx = canvas.getContext("2d");
        ctx.textBaseline = "alphabetic";

        // Counted before the bitmaps are closed
        const missing = photos.filter(p => !bitmaps.has(p.photoId)).length;
        let extra;
        try {
            extra = paint(ctx, size, bitmaps);
        } finally {
            closeAll(bitmaps);
        }

        const blob = await toBlob(canvas);
        if (canvas.stiklingToken !== token || !blob)
            return null;

        const old = kept.get(spec.id);
        if (old)
            URL.revokeObjectURL(old.url);
        const url = URL.createObjectURL(blob);
        kept.set(spec.id, { blob, url });
        return { url, missing, ...extra };
    }

    // Lets go of the pictures drawn under these ids
    function release(ids) {
        for (const id of ids) {
            const entry = kept.get(id);
            if (entry) {
                URL.revokeObjectURL(entry.url);
                kept.delete(id);
            }
        }
    }

    // The JPEG kept under this id, or undefined
    function blobOf(id) {
        return kept.get(id)?.blob;
    }

    function fileOf(id, fileName) {
        const entry = kept.get(id);
        return entry ? new File([entry.blob], fileName, { type: "image/jpeg" }) : null;
    }

    // Whether the browser can share this many pictures in one go
    function canShareAll(count) {
        const files = Array.from({ length: count }, (_, i) => new File([new Blob()], `card-${i}.jpg`, { type: "image/jpeg" }));
        return canShareFiles(files);
    }

    // One picture through the share menu when the browser allows it, otherwise saved.
    // "shared", "cancelled" (closed the share menu), "saved" (it couldn't share, so it was saved) or "failed"
    async function share(id, fileName) {
        const file = fileOf(id, fileName);
        if (!file)
            return "failed";
        if (canShareFiles([file])) {
            try {
                await navigator.share({ files: [file] });
                return "shared";
            } catch (error) {
                if (error?.name === "AbortError")
                    return "cancelled";
                // Something else went wrong with sharing, so fall back to saving it
            }
        }
        return await keep(file, file.name);
    }

    // "saved" or "failed"
    async function save(id, fileName) {
        const entry = kept.get(id);
        return entry ? await keep(entry.blob, fileName) : "failed";
    }

    // All the pictures through one share menu when the browser allows it, otherwise saved one by one.
    // "shared", "cancelled" (closed the share menu), "saved" or "failed"
    async function shareAll(ids, fileNames) {
        const files = filesOf(ids, fileNames);
        if (files.length === 0)
            return "failed";
        if (canShareFiles(files)) {
            try {
                await navigator.share({ files });
                return "shared";
            } catch (error) {
                if (error?.name === "AbortError")
                    return "cancelled";
                // Something else went wrong with sharing, so fall back to saving them
            }
        }
        return await saveAll(files);
    }

    // "saved" or "failed"
    async function saveEach(ids, fileNames) {
        const files = filesOf(ids, fileNames);
        return files.length === 0 ? "failed" : await saveAll(files);
    }

    function filesOf(ids, fileNames) {
        return ids.map((id, i) => fileOf(id, fileNames[i])).filter(Boolean);
    }

    return { draw, release, blobOf, canShareAll, share, save, shareAll, saveEach };
}

// The full photo, or its thumbnail when the full one is not on this device. Each photo is decoded
// once, even when the card uses it in two places. Missing ones are left out of the map.
async function readPhotos(refs) {
    const bitmaps = new Map();
    for (const { photoId } of refs) {
        if (bitmaps.has(photoId))
            continue;
        const blob = (await getBlob(photoId)) ?? (await getBlob(`${photoId}:thumb`));
        if (!blob)
            continue;
        try {
            bitmaps.set(photoId, await createImageBitmap(blob));
        } catch {
            // Treated as missing
        }
    }
    return bitmaps;
}

function closeAll(bitmaps) {
    for (const bitmap of bitmaps.values())
        bitmap.close();
}

function canShareFiles(files) {
    try {
        return typeof navigator.share === "function" && navigator.canShare?.({ files }) === true;
    } catch {
        return false;
    }
}

async function saveAll(files) {
    for (let i = 0; i < files.length; i++) {
        if (i > 0)
            await new Promise(resolve => setTimeout(resolve, 400));
        await keep(files[i], files[i].name);
    }
    return "saved";
}
