// An experiment's result as a picture: the mediums one under the other, each with its photo, how
// many rooted and a bar for how long it took. Drawn on a canvas in the look of the other cards
// (sharecard.js), and kept here as a JPEG for the phone's share menu or for saving. The drawing
// shell, reading the photos and the sharing are in cardkit.js; this file has the layout. The photos
// are read in JavaScript and never cross into .NET; C# only sends the words, the photo ids and their
// frames.

import {
    SURFACE, BORDER, INK, MUTED, POTTING_GREEN, LEAF_TINT,
    DISPLAY, BODY, MARGIN, ICONS,
    setFont, wrap, paintIcon, paintFooter, drawCover,
} from "./sharecard.js";
import { cardKit } from "./cardkit.js";

const kit = cardKit();

const KICKER_SIZE = 28;
const PHOTO_CORNER = 14;
const ROW_PAD = 14;
const MAX_PHOTO = 170;
const BAR_HEIGHT = 12;
const FOOTER = 32;
const FOOTER_BOTTOM = 52;

// Lets go of the pictures drawn under these ids
export const release = kit.release;

// Draws the picture on the canvas. Returns { url, missing }: an object URL of the JPEG and how many
// photos were not on this device. Null when a newer draw on this canvas has taken over before this
// one finished.
// spec: { id, format, kicker, title, more, rows: [{ name, result, days, photo: { photoId, frame } | null }] }
// days is the average days to root, or null for a medium with no bar; more is "and 3 more" or null.
export function draw(canvas, spec) {
    const rows = spec.rows ?? [];
    return kit.draw(canvas, spec, {
        format: "square",
        photos: rows.map(r => r.photo).filter(Boolean),
        paint: (ctx, size, photos) => paint(ctx, size, spec, rows, photos),
    });
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

export const share = kit.share;
export const save = kit.save;
