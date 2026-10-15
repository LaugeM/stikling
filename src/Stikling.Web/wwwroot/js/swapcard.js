// The swap list as pictures: a grid of photos, each with its name and count under it, drawn on a
// canvas in the look of the year collage card (yearcards.js), and kept here as JPEG files for the
// phone's share menu or for saving. The photos are read here and never cross into .NET; C# only
// sends the words, the photo ids and their frames.

import { getBlob } from "./db.js";
import {
    SIZES, SURFACE, INK, MUTED, POTTING_GREEN, LEAF_TINT, CLAY, CLAY_SOFT,
    DISPLAY, BODY, PAIR_GAP, ICONS,
    setFont, wrap, paintPhotoCard, paintIcon, drawCover, loadFonts, toBlob, keep,
} from "./sharecard.js";

// The same size of words on every format, set to be read on a phone
const NAME_SIZE = 30;
const DETAIL_SIZE = 25;
const STRIP_PAD = 14;
const NAME_LINES = 2;
const STRIP_HEIGHT = Math.round(STRIP_PAD + NAME_LINES * NAME_SIZE * 1.12 + 6 + DETAIL_SIZE * 1.2 + STRIP_PAD);

// The pictures drawn, by id, for sharing and saving
const kept = new Map(); // id to { blob, url }

// Draws one picture on the canvas. Returns { url, missing }: an object URL of the JPEG and how many
// photos were not on this device. Null when a newer draw on this canvas has taken over before this
// one finished.
// spec: { id, format, title, line, tiles: [{ name, detail, tone, photo: { photoId, frame } | null }] }
export async function draw(canvas, spec) {
    const token = canvas.stiklingToken = (canvas.stiklingToken ?? 0) + 1;
    const size = SIZES[spec.format] ?? SIZES.square;
    const tiles = spec.tiles ?? [];

    await loadFonts();
    const photos = await readPhotos(tiles);
    if (canvas.stiklingToken !== token) {
        closeAll(photos);
        return null;
    }

    canvas.width = size.width;
    canvas.height = size.height;
    const ctx = canvas.getContext("2d");
    ctx.textBaseline = "alphabetic";

    // Counted before the bitmaps are closed
    const missing = tiles.filter(t => t.photo && !photos.has(t.photo.photoId)).length;
    try {
        paintGrid(ctx, size, spec, tiles, photos);
    } finally {
        closeAll(photos);
    }

    const blob = await toBlob(canvas);
    if (canvas.stiklingToken !== token || !blob)
        return null;

    const old = kept.get(spec.id);
    if (old)
        URL.revokeObjectURL(old.url);
    const url = URL.createObjectURL(blob);
    kept.set(spec.id, { blob, url });
    return { url, missing };
}

// Lets go of the pictures drawn under these ids
export function release(ids) {
    for (const id of ids) {
        const entry = kept.get(id);
        if (entry) {
            URL.revokeObjectURL(entry.url);
            kept.delete(id);
        }
    }
}

// The full photo, or its thumbnail when the full one is not on this device. Missing ones are left out.
async function readPhotos(tiles) {
    const photos = new Map();
    for (const { photo } of tiles) {
        if (!photo || photos.has(photo.photoId))
            continue;
        const blob = (await getBlob(photo.photoId)) ?? (await getBlob(`${photo.photoId}:thumb`));
        if (!blob)
            continue;
        try {
            photos.set(photo.photoId, await createImageBitmap(blob));
        } catch {
            // Treated as missing
        }
    }
    return photos;
}

function closeAll(photos) {
    for (const bitmap of photos.values())
        bitmap.close();
}

// ---- Layout ----

// The grid edge to edge from the top, the band with the title under it
function paintGrid(ctx, size, spec, tiles, photos) {
    const words = { name: spec.title, line: spec.line };
    paintPhotoCard(ctx, size, words, bandTop => {
        ctx.fillStyle = SURFACE;
        ctx.fillRect(0, 0, size.width, bandTop);
        if (tiles.length === 0)
            return;

        const rowSizes = rowsFor(tiles.length);
        const widest = Math.max(...rowSizes);
        const cellWidthMin = (size.width - (widest - 1) * PAIR_GAP) / widest;
        // With only a few tiles the rows would be taller than they are wide, so they stop growing
        const fill = (bandTop - (rowSizes.length - 1) * PAIR_GAP) / rowSizes.length;
        const rowHeight = Math.min(fill, cellWidthMin * 1.4 + STRIP_HEIGHT);

        let next = 0;
        rowSizes.forEach((count, row) => {
            const y = Math.round(row * (rowHeight + PAIR_GAP));
            const h = Math.round((row + 1) * rowHeight + row * PAIR_GAP) - y;
            const cellWidth = (size.width - (count - 1) * PAIR_GAP) / count;
            for (let i = 0; i < count; i++) {
                const x = Math.round(i * (cellWidth + PAIR_GAP));
                const w = Math.round((i + 1) * cellWidth + i * PAIR_GAP) - x;
                paintTile(ctx, tiles[next++], photos, x, y, w, h);
            }
        });
    });
}

// Two across for four or fewer, else three, in rows that share the tiles out evenly
function rowsFor(count) {
    const perRow = count <= 4 ? 2 : 3;
    const rows = Math.max(1, Math.ceil(count / perRow));
    const base = Math.floor(count / rows);
    const extra = count - base * rows;
    return Array.from({ length: rows }, (_, i) => base + (i < extra ? 1 : 0));
}

function paintTile(ctx, tile, photos, x, y, w, h) {
    const photoHeight = h - STRIP_HEIGHT;
    const bitmap = tile.photo && photos.get(tile.photo.photoId);
    if (bitmap) {
        drawCover(ctx, bitmap, tile.photo.frame, x, y, w, photoHeight);
    } else {
        // The leaf on the tone's colour, as the thumbnails in the app show it
        const plant = tile.tone === "plant";
        ctx.fillStyle = plant ? CLAY_SOFT : LEAF_TINT;
        ctx.fillRect(x, y, w, photoHeight);
        const extent = Math.min(w, photoHeight) * 0.4;
        paintIcon(ctx, ICONS.plant, x + (w - extent) / 2, y + (photoHeight - extent) / 2, extent, plant ? CLAY : POTTING_GREEN);
    }

    const textWidth = w - 2 * STRIP_PAD;
    const top = y + photoHeight;
    ctx.fillStyle = SURFACE;
    ctx.fillRect(x, top, w, STRIP_HEIGHT);

    setFont(ctx, `700 ${NAME_SIZE}px ${DISPLAY}`, "-0.02em", "semi-condensed");
    ctx.fillStyle = INK;
    const lines = wrap(ctx, tile.name, textWidth, NAME_LINES);
    lines.forEach((text, i) => ctx.fillText(text, x + STRIP_PAD, top + STRIP_PAD + NAME_SIZE * 0.9 + i * NAME_SIZE * 1.12));

    setFont(ctx, `400 ${DETAIL_SIZE}px ${BODY}`, "0", "normal");
    ctx.fillStyle = MUTED;
    const [detail] = wrap(ctx, tile.detail, textWidth, 1);
    ctx.fillText(detail ?? "", x + STRIP_PAD, top + STRIP_HEIGHT - STRIP_PAD - DETAIL_SIZE * 0.2);
}

// ---- Sharing and saving ----

function filesOf(ids, fileNames) {
    return ids
        .map((id, i) => {
            const entry = kept.get(id);
            return entry ? new File([entry.blob], fileNames[i], { type: "image/jpeg" }) : null;
        })
        .filter(Boolean);
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

// All the pictures through one share menu when the browser allows it, otherwise saved one by one.
// "shared", "cancelled" (closed the share menu), "saved" or "failed"
export async function shareAll(ids, fileNames) {
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
export async function saveEach(ids, fileNames) {
    const files = filesOf(ids, fileNames);
    return files.length === 0 ? "failed" : await saveAll(files);
}
