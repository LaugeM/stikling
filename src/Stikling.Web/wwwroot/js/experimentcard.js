// An experiment's result as a picture: the mediums one under the other, each with its photo, how
// many rooted and a bar for how long it took. Drawn on a canvas in the look of the other cards
// (sharecard.js), and kept here as a JPEG for the phone's share menu or for saving. The photos are
// read here and never cross into .NET; C# only sends the words, the photo ids and their frames.

import { getBlob } from "./db.js";
import {
    SIZES, SURFACE, BORDER, INK, MUTED, POTTING_GREEN, LEAF_TINT,
    DISPLAY, BODY, MARGIN, ICONS,
    setFont, wrap, paintIcon, paintFooter, drawCover, loadFonts, toBlob, keep,
} from "./sharecard.js";

const KICKER_SIZE = 28;
const PHOTO_CORNER = 14;
const ROW_PAD = 14;
const MAX_PHOTO = 170;
const BAR_HEIGHT = 12;
const FOOTER = 32;
const FOOTER_BOTTOM = 52;

// The pictures drawn, by id, for sharing and saving
const kept = new Map(); // id to { blob, url }

// Draws the picture on the canvas. Returns { url, missing }: an object URL of the JPEG and how many
// photos were not on this device. Null when a newer draw on this canvas has taken over before this
// one finished.
// spec: { id, format, kicker, title, more, rows: [{ name, result, days, photo: { photoId, frame } | null }] }
// days is the average days to root, or null for a medium with no bar; more is "and 3 more" or null.
export async function draw(canvas, spec) {
    const token = canvas.stiklingToken = (canvas.stiklingToken ?? 0) + 1;
    const size = SIZES[spec.format] ?? SIZES.square;
    const rows = spec.rows ?? [];

    await loadFonts();
    const photos = await readPhotos(rows);
    if (canvas.stiklingToken !== token) {
        closeAll(photos);
        return null;
    }

    canvas.width = size.width;
    canvas.height = size.height;
    const ctx = canvas.getContext("2d");
    ctx.textBaseline = "alphabetic";

    // Counted before the bitmaps are closed
    const missing = rows.filter(r => r.photo && !photos.has(r.photo.photoId)).length;
    try {
        paint(ctx, size, spec, rows, photos);
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
async function readPhotos(rows) {
    const photos = new Map();
    for (const { photo } of rows) {
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

// One line, cut with an ellipsis when it is too wide
function fit(ctx, text, maxWidth) {
    return wrap(ctx, text, maxWidth, 1)[0] ?? "";
}

function paint(ctx, size, spec, rows, photos) {
    const { width, height } = size;
    const textWidth = width - 2 * MARGIN;
    const safeBottom = Math.round(height * (size.safeBottom ?? 1));

    ctx.fillStyle = SURFACE;
    ctx.fillRect(0, 0, width, height);
    if (safeBottom < height) {
        ctx.fillStyle = BORDER;
        ctx.fillRect(0, safeBottom - 2, width, 2);
    }

    // Kicker
    let y = 84;
    setFont(ctx, `600 ${KICKER_SIZE}px ${BODY}`, "0.06em", "normal");
    ctx.fillStyle = MUTED;
    ctx.fillText(fit(ctx, (spec.kicker ?? "").toLocaleUpperCase(), textWidth), MARGIN, y + KICKER_SIZE * 0.8);
    y += KICKER_SIZE + 18;

    // The name, up to two lines
    const titleSize = size.name;
    setFont(ctx, `700 ${titleSize}px ${DISPLAY}`, "-0.02em", "semi-condensed");
    ctx.fillStyle = INK;
    const lines = wrap(ctx, spec.title ?? "", textWidth, 2);
    lines.forEach((line, i) => ctx.fillText(line, MARGIN, y + titleSize * 0.9 + i * titleSize * 1.1));
    y += lines.length * titleSize * 1.1 + 36;

    // The footer sits at the bottom of the readable part
    const footerBaseline = safeBottom - FOOTER_BOTTOM;
    paintFooter(ctx, width - MARGIN, footerBaseline, FOOTER);

    if (rows.length === 0)
        return;

    // Rows share the space between the name and the footer
    const moreHeight = spec.more ? 64 : 0;
    const bottom = footerBaseline - FOOTER - 28;
    const rowHeight = Math.min((bottom - y - moreHeight) / rows.length, MAX_PHOTO + 2 * ROW_PAD);
    const photoSize = Math.max(60, Math.floor(rowHeight - 2 * ROW_PAD));
    const longest = Math.max(1, ...rows.map(r => r.days ?? 0));

    rows.forEach((row, i) => {
        const top = Math.round(y + i * rowHeight);
        if (i > 0) {
            ctx.fillStyle = BORDER;
            ctx.fillRect(MARGIN, top, textWidth, 2);
        }
        paintRow(ctx, row, photos, longest, MARGIN, top + Math.round((rowHeight - photoSize) / 2), photoSize, textWidth);
    });

    if (spec.more) {
        setFont(ctx, `400 ${KICKER_SIZE + 4}px ${BODY}`, "0", "normal");
        ctx.fillStyle = MUTED;
        ctx.fillText(fit(ctx, spec.more, textWidth), MARGIN, Math.round(y + rows.length * rowHeight + 40));
    }
}

function paintRow(ctx, row, photos, longest, x, y, photoSize, rowWidth) {
    // The photo, or Leaf Tint with the stroked glass
    ctx.save();
    ctx.beginPath();
    ctx.roundRect(x, y, photoSize, photoSize, PHOTO_CORNER);
    ctx.clip();
    const bitmap = row.photo && photos.get(row.photo.photoId);
    if (bitmap) {
        drawCover(ctx, bitmap, row.photo.frame, x, y, photoSize, photoSize);
    } else {
        ctx.fillStyle = LEAF_TINT;
        ctx.fillRect(x, y, photoSize, photoSize);
        const extent = photoSize * 0.4;
        paintIcon(ctx, ICONS.propagation, x + (photoSize - extent) / 2, y + (photoSize - extent) / 2, extent, POTTING_GREEN);
    }
    ctx.restore();

    const left = x + photoSize + 30;
    const width = x + rowWidth - left;
    const nameSize = Math.min(40, Math.max(28, Math.round(photoSize * 0.3)));
    const resultSize = Math.round(nameSize * 0.85);
    const hasBar = row.days != null;
    const block = nameSize * 1.1 + 6 + resultSize * 1.1 + (hasBar ? 12 + BAR_HEIGHT : 0);
    const top = y + (photoSize - block) / 2;

    setFont(ctx, `700 ${nameSize}px ${DISPLAY}`, "-0.01em", "semi-condensed");
    ctx.fillStyle = INK;
    ctx.fillText(fit(ctx, row.name, width), left, top + nameSize * 0.85);

    const resultTop = top + nameSize * 1.1 + 6;
    setFont(ctx, `400 ${resultSize}px ${BODY}`, "0", "normal");
    ctx.fillStyle = MUTED;
    ctx.fillText(fit(ctx, row.result, width), left, resultTop + resultSize * 0.85);

    if (hasBar) {
        const barWidth = Math.max(BAR_HEIGHT, Math.round(width * Math.min(1, row.days / longest)));
        ctx.fillStyle = POTTING_GREEN;
        ctx.beginPath();
        ctx.roundRect(left, resultTop + resultSize * 1.1 + 12, barWidth, BAR_HEIGHT, BAR_HEIGHT / 2);
        ctx.fill();
    }
}

// ---- Sharing and saving ----

function fileOf(id, fileName) {
    const entry = kept.get(id);
    return entry ? new File([entry.blob], fileName, { type: "image/jpeg" }) : null;
}

function canShareFiles(files) {
    try {
        return typeof navigator.share === "function" && navigator.canShare?.({ files }) === true;
    } catch {
        return false;
    }
}

// "shared", "cancelled" (closed the share menu), "saved" (it couldn't share, so it was saved) or "failed"
export async function share(id, fileName) {
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
    await keep(file, file.name);
    return "saved";
}

// "saved" or "failed"
export async function save(id, fileName) {
    const file = fileOf(id, fileName);
    if (!file)
        return "failed";
    await keep(file, file.name);
    return "saved";
}
