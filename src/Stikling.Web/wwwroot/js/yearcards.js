// The cards of "Your year with your plants": pictures drawn on a canvas, kept here as JPEG files and
// handed to the phone's share menu or saved. The photos are read here and never cross into .NET;
// C# only sends the words and the photo ids.
//
// The cards follow the share card (sharecard.js): always light, 1080 wide, the same sizes, and in
// 9:16 everything that is read stays above the safe line.

import { getBlob, hasBlob } from "./db.js";
import {
    SIZES, SURFACE, INK, MUTED, DEEP_MOSS, POTTING_GREEN, LEAF_TINT, CLAY, CLAY_SOFT,
    DISPLAY, BODY, MARGIN, PAIR_GAP, PILL_INSET, ICONS,
    setFont, wrap, paintFooter, paintIcon, paintPhotoCard, paintDatePill, drawCover, loadFonts, toBlob, keep,
} from "./sharecard.js";

// The picture of each card that was drawn, by card id, for sharing and saving
const kept = new Map(); // id to { blob, url }

// Which of these photos are only small on this device, and which aren't here at all
export async function check(ids) {
    const small = [];
    const missing = [];
    for (const id of ids) {
        if (await hasBlob(id))
            continue;
        if (await hasBlob(`${id}:thumb`))
            small.push(id);
        else
            missing.push(id);
    }
    return { small, missing };
}

// Draws one card on the canvas. Returns { url, missing, cells }: an object URL of the JPEG, how many
// photos weren't on this device, and for a collage each photo's box as fractions of the card. Null
// when a newer draw on this canvas has taken over before this one finished.
// spec: the card (id, kind, tone, kicker, title, latin, cultivar, line, figures, photo, before, after,
// beforeLabel, afterLabel) with format, and for the collage the photos to draw.
export async function draw(canvas, spec) {
    const token = canvas.stiklingToken = (canvas.stiklingToken ?? 0) + 1;
    const size = SIZES[spec.format] ?? SIZES.story;

    await loadFonts();
    const photos = await readPhotos(wantedBy(spec));
    if (canvas.stiklingToken !== token) {
        closeAll(photos);
        return null;
    }

    canvas.width = size.width;
    canvas.height = size.height;
    const ctx = canvas.getContext("2d");
    ctx.textBaseline = "alphabetic";

    let cells = null;
    try {
        if (spec.kind === "Title")
            paintTitle(ctx, size, spec, photos);
        else if (spec.kind === "Number")
            paintNumber(ctx, size, spec);
        else if (spec.kind === "Photo")
            paintPhoto(ctx, size, spec, photos);
        else if (spec.kind === "Pair")
            paintPair(ctx, size, spec, photos);
        else
            cells = paintCollage(ctx, size, spec, photos);
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

    const missing = wantedBy(spec).filter(w => !photos.get(w.photoId)).length;
    return { url, missing, cells };
}

// Lets go of the pictures drawn under these ids. Today and the year page each pass their own,
// so leaving one page never blanks a picture the other is showing.
export function release(ids) {
    for (const id of ids) {
        const entry = kept.get(id);
        if (entry) {
            URL.revokeObjectURL(entry.url);
            kept.delete(id);
        }
    }
}

// ---- Which photos a card needs, and reading them ----

function wantedBy(spec) {
    const list = [];
    if (spec.kind === "Pair")
        list.push(spec.before, spec.after);
    else if (spec.kind === "Collage")
        list.push(...(spec.collage ?? []));
    else if (spec.photo)
        list.push(spec.photo);
    return list.filter(Boolean);
}

// A photo is decoded once per card, even when two places on the card use it. The thumbnail stands in
// when the full photo isn't on this device. Missing ones are left out of the map.
async function readPhotos(wanted) {
    const photos = new Map();
    for (const { photoId } of wanted) {
        if (photos.has(photoId))
            continue;
        const blob = (await getBlob(photoId)) ?? (await getBlob(`${photoId}:thumb`));
        if (!blob)
            continue;
        try {
            photos.set(photoId, await createImageBitmap(blob));
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

// ---- Layouts ----

const isPlant = spec => spec.tone === "plant";
const toneFill = spec => (isPlant(spec) ? CLAY_SOFT : LEAF_TINT);
const toneInk = spec => (isPlant(spec) ? CLAY : POTTING_GREEN);

// A photo that isn't on this device: the card's colour with its stroked icon, as the share card does
function paintMissing(ctx, spec, x, y, w, h) {
    ctx.fillStyle = toneFill(spec);
    ctx.fillRect(x, y, w, h);
    const extent = Math.min(w, h) * 0.4;
    paintIcon(ctx, ICONS[isPlant(spec) ? "plant" : "propagation"], x + (w - extent) / 2, y + (h - extent) / 2, extent, toneInk(spec));
}

function paintPhotoIn(ctx, spec, photos, side, x, y, w, h) {
    const bitmap = side && photos.get(side.photoId);
    if (bitmap)
        drawCover(ctx, bitmap, side.frame, x, y, w, h);
    else
        paintMissing(ctx, spec, x, y, w, h);
}

function words(spec) {
    return {
        name: spec.title,
        latin: spec.latin,
        cultivar: spec.cultivar,
        line: spec.line,
        kicker: spec.kicker,
        figures: spec.figures?.length ? spec.figures : null,
    };
}

// The title: a photo above and the name with its numbers in the band, or, when the photo isn't
// here, the name and numbers centred on Leaf Tint
function paintTitle(ctx, size, spec, photos) {
    const bitmap = spec.photo && photos.get(spec.photo.photoId);
    if (bitmap) {
        paintPhotoCard(ctx, size, words(spec), bandTop => drawCover(ctx, bitmap, spec.photo.frame, 0, 0, size.width, bandTop));
        return;
    }

    const { width, height } = size;
    const textWidth = width - 2 * MARGIN;
    const safeBottom = Math.round(height * (size.safeBottom ?? 1));
    ctx.fillStyle = LEAF_TINT;
    ctx.fillRect(0, 0, width, height);

    const titleSize = Math.round(size.name * 1.3);
    setFont(ctx, `700 ${titleSize}px ${DISPLAY}`, "-0.02em", "semi-condensed");
    const lines = wrap(ctx, spec.title, textWidth, 3);
    const titleHeight = lines.length * titleSize * 1.1;

    const figures = spec.figures ?? [];
    const numSize = Math.round(size.figure * 0.4);
    const capSize = Math.round(size.latin * 0.9);
    const figuresHeight = figures.length ? 70 + numSize * 0.72 + 16 + capSize * 1.2 : 0;
    const total = titleHeight + figuresHeight;
    const top = Math.max(MARGIN, (safeBottom - MARGIN - 32 - total) / 2);

    ctx.fillStyle = INK;
    ctx.textAlign = "center";
    setFont(ctx, `700 ${titleSize}px ${DISPLAY}`, "-0.02em", "semi-condensed");
    lines.forEach((text, i) => ctx.fillText(text, width / 2, top + titleSize * 0.9 + i * titleSize * 1.1));

    const column = textWidth / Math.max(1, figures.length);
    figures.forEach((figure, i) => {
        const centre = MARGIN + column * (i + 0.5);
        const baseline = top + titleHeight + 70 + numSize * 0.72;
        let fontSize = numSize;
        setFont(ctx, `800 ${fontSize}px ${DISPLAY}`, "-0.02em", "condensed");
        while (ctx.measureText(figure.value).width > column - 24 && fontSize > numSize * 0.5) {
            fontSize -= 4;
            setFont(ctx, `800 ${fontSize}px ${DISPLAY}`, "-0.02em", "condensed");
        }
        ctx.fillStyle = DEEP_MOSS;
        ctx.fillText(figure.value, centre, baseline);

        let captionSize = capSize;
        setFont(ctx, `600 ${captionSize}px ${DISPLAY}`, "0", "normal");
        while (ctx.measureText(figure.caption).width > column - 24 && captionSize > capSize * 0.6) {
            captionSize -= 2;
            setFont(ctx, `600 ${captionSize}px ${DISPLAY}`, "0", "normal");
        }
        ctx.fillStyle = MUTED;
        ctx.fillText(figure.caption, centre, baseline + 16 + captionSize);
    });
    ctx.textAlign = "left";
    paintFooter(ctx, width - MARGIN, safeBottom - MARGIN, 32);
}

// A big number with its words, a hairline and the line, like the share card without a photo
function paintNumber(ctx, size, spec) {
    const { width, height } = size;
    const plant = isPlant(spec);
    const safeBottom = Math.round(height * (size.safeBottom ?? 1));
    const textWidth = width - 2 * MARGIN;

    ctx.fillStyle = toneFill(spec);
    ctx.fillRect(0, 0, width, height);

    const footer = 32;
    const footerBaseline = safeBottom - MARGIN;
    paintFooter(ctx, width - MARGIN, footerBaseline, footer);

    // The kicker, small and upper case, at the top margin
    const kickerSize = Math.round(size.latin * 0.85);
    let kickerFont = kickerSize;
    const kicker = (spec.kicker ?? "").toUpperCase();
    setFont(ctx, `600 ${kickerFont}px ${BODY}`, "0.06em", "normal");
    while (ctx.measureText(kicker).width > textWidth && kickerFont > kickerSize * 0.6) {
        kickerFont -= 2;
        setFont(ctx, `600 ${kickerFont}px ${BODY}`, "0.06em", "normal");
    }
    ctx.fillStyle = MUTED;
    ctx.fillText(kicker, MARGIN, MARGIN + kickerSize);

    // The line at the bottom of the safe area, under a hairline
    if (spec.line) {
        const lineSize = Math.round(size.line * 1.25);
        const lineHeight = lineSize * 1.25;
        setFont(ctx, `600 ${lineSize}px ${DISPLAY}`, "0", "normal");
        const lines = wrap(ctx, spec.line, textWidth, size.lineLines);
        const blockHeight = lines.length * lineHeight;
        const blockTop = footerBaseline - footer - 36 - blockHeight;
        ctx.fillStyle = plant ? "#e6cdbd" : "#c9ded0";
        ctx.fillRect(MARGIN, blockTop - 44, textWidth, 2);
        ctx.fillStyle = INK;
        lines.forEach((text, i) => ctx.fillText(text, MARGIN, blockTop + lineSize * 0.9 + i * lineHeight));
    }

    const figure = spec.figures?.[0];
    if (!figure)
        return;
    let fontSize = size.figure;
    ctx.fillStyle = plant ? CLAY : DEEP_MOSS;
    setFont(ctx, `800 ${fontSize}px ${DISPLAY}`, "-0.02em", "condensed");
    while (ctx.measureText(figure.value).width > textWidth && fontSize > 120) {
        fontSize -= 20;
        setFont(ctx, `800 ${fontSize}px ${DISPLAY}`, "-0.02em", "condensed");
    }
    const baseline = MARGIN + kickerSize + 70 + ctx.measureText(figure.value).actualBoundingBoxAscent;
    ctx.fillText(figure.value, MARGIN, baseline);

    const captionSize = Math.round(fontSize * 0.15);
    setFont(ctx, `600 ${captionSize}px ${DISPLAY}`, "0", "normal");
    ctx.fillStyle = INK;
    wrap(ctx, figure.caption, textWidth - 6, 2).forEach((text, i) =>
        ctx.fillText(text, MARGIN + 6, baseline + captionSize * 1.5 + i * captionSize * 1.3));
}

// One plant or propagation: the photo above, the kicker and name in the band under it
function paintPhoto(ctx, size, spec, photos) {
    paintPhotoCard(ctx, size, words(spec), bandTop =>
        paintPhotoIn(ctx, spec, photos, spec.photo, 0, 0, size.width, bandTop));
}

// The same plant twice, the older photo first, each with its date
function paintPair(ctx, size, spec, photos) {
    paintPhotoCard(ctx, size, words(spec), bandTop => {
        // Side by side, but stacked on a story, which is tall and narrow
        const stacked = size.height > size.width * 1.5;
        const boxW = stacked ? size.width : Math.floor((size.width - PAIR_GAP) / 2);
        const boxH = stacked ? Math.floor((bandTop - PAIR_GAP) / 2) : bandTop;
        const boxes = stacked
            ? [[0, 0, boxW, boxH], [0, boxH + PAIR_GAP, boxW, bandTop - boxH - PAIR_GAP]]
            : [[0, 0, boxW, boxH], [boxW + PAIR_GAP, 0, size.width - boxW - PAIR_GAP, boxH]];

        // The gap shows the band's colour
        ctx.fillStyle = SURFACE;
        ctx.fillRect(0, 0, size.width, bandTop);
        [[spec.before, spec.beforeLabel], [spec.after, spec.afterLabel]].forEach(([side, label], i) => {
            const [x, y, w, h] = boxes[i];
            paintPhotoIn(ctx, spec, photos, side, x, y, w, h);
            const scale = stacked ? 1.25 : 1;
            paintDatePill(ctx, label, x + PILL_INSET * scale, y + h - PILL_INSET * scale, w - 2 * PILL_INSET * scale, scale);
        });
    });
}

// The photos edge to edge from the top in a grid, the band under them. Three across, in rows that
// share the photos out evenly, so a year with few photos still fills the grid.
function paintCollage(ctx, size, spec, photos) {
    const shown = spec.collage ?? [];
    const rowSizes = collageRows(shown.length, spec.format === "square" ? 3 : 4);
    const cells = [];

    paintPhotoCard(ctx, size, words(spec), bandTop => {
        ctx.fillStyle = SURFACE;
        ctx.fillRect(0, 0, size.width, bandTop);

        const rowHeight = (bandTop - (rowSizes.length - 1) * PAIR_GAP) / rowSizes.length;
        let next = 0;
        rowSizes.forEach((count, row) => {
            const y = Math.round(row * (rowHeight + PAIR_GAP));
            const h = Math.round((row + 1) * rowHeight + row * PAIR_GAP) - y;
            const cellWidth = (size.width - (count - 1) * PAIR_GAP) / count;
            for (let i = 0; i < count; i++) {
                const x = Math.round(i * (cellWidth + PAIR_GAP));
                const w = Math.round((i + 1) * cellWidth + i * PAIR_GAP) - x;
                paintPhotoIn(ctx, spec, photos, shown[next++], x, y, w, h);
                cells.push({ x: x / size.width, y: y / size.height, w: w / size.width, h: h / size.height });
            }
        });
    });
    return cells;
}

// How many photos go in each row: at most three across, at most maxRows rows, shared out evenly
function collageRows(count, maxRows) {
    const perRow = count <= 4 ? 2 : 3;
    const rows = Math.min(maxRows, Math.max(1, Math.ceil(count / perRow)));
    const base = Math.floor(count / rows);
    const extra = count - base * rows;
    return Array.from({ length: rows }, (_, i) => base + (i < extra ? 1 : 0));
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

// Whether the browser can share this many pictures in one go
export function canShareAll(count) {
    const files = Array.from({ length: count }, (_, i) => new File([new Blob()], `card-${i}.jpg`, { type: "image/jpeg" }));
    return canShareFiles(files);
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
    return await keep(file, fileName);
}

// "saved" or "failed"
export async function save(id, fileName) {
    const entry = kept.get(id);
    return entry ? await keep(entry.blob, fileName) : "failed";
}

// All the pictures through one share menu when the browser allows it, otherwise saved one by one
export async function shareAll(ids, fileNames) {
    const files = ids.map((id, i) => fileOf(id, fileNames[i])).filter(Boolean);
    if (files.length === 0)
        return "failed";
    if (canShareFiles(files)) {
        try {
            await navigator.share({ files });
            return "shared";
        } catch (error) {
            if (error?.name === "AbortError")
                return "cancelled";
        }
    }

    for (let i = 0; i < files.length; i++) {
        if (i > 0)
            await new Promise(resolve => setTimeout(resolve, 400));
        await keep(files[i], files[i].name);
    }
    return "saved";
}

// ---- Swiping through the deck ----

// The front card follows the finger along the stage. A drag past 50px, or a fast flick, calls
// Swipe(+1 or -1, how far it had moved) on the page, and anything less springs back. Vertical
// movement is left to the browser, so the page still scrolls.
export function attachDeck(element, dotNetRef) {
    const front = () => element.querySelector("[data-deck-front]");
    let pointer = null; // { id, x, y, at, dragging }

    const onDown = event => {
        if (event.pointerType === "mouse" && event.button !== 0)
            return;
        pointer = { id: event.pointerId, x: event.clientX, y: event.clientY, at: performance.now(), dragging: false };
    };

    const onMove = event => {
        if (!pointer || event.pointerId !== pointer.id)
            return;
        const dx = event.clientX - pointer.x;
        const dy = event.clientY - pointer.y;
        if (!pointer.dragging) {
            if (Math.abs(dy) > 10 && Math.abs(dy) > Math.abs(dx)) {
                pointer = null;
                return;
            }
            if (Math.abs(dx) < 8 || Math.abs(dx) < Math.abs(dy) * 1.2)
                return;
            pointer.dragging = true;
            try {
                element.setPointerCapture(event.pointerId);
            } catch {
                // The drag still works without it
            }
            const card = front();
            if (card)
                card.style.transition = "none";
        }
        const card = front();
        if (card)
            card.style.transform = `translateX(${dx}px)`;
    };

    const onUp = event => {
        if (!pointer || event.pointerId !== pointer.id)
            return;
        const { dragging, x, at } = pointer;
        pointer = null;
        if (!dragging)
            return;

        // The tap that ends a drag isn't a tap on whatever is under the finger
        const swallow = e => e.stopPropagation();
        element.addEventListener("click", swallow, { capture: true, once: true });
        setTimeout(() => element.removeEventListener("click", swallow, { capture: true }), 0);

        const dx = event.clientX - x;
        const speed = Math.abs(dx) / Math.max(1, performance.now() - at);
        const card = front();
        if (Math.abs(dx) > 50 || (speed > 0.5 && Math.abs(dx) > 20)) {
            if (card) {
                card.style.transition = "none";
                card.style.transform = "";
            }
            dotNetRef.invokeMethodAsync("Swipe", dx < 0 ? 1 : -1, dx);
        } else if (card) {
            // Springs back
            card.style.transition = "transform 180ms ease-out";
            card.style.transform = "";
        }
    };

    const onCancel = event => {
        if (!pointer || event.pointerId !== pointer.id)
            return;
        const card = front();
        if (pointer.dragging && card) {
            card.style.transition = "transform 180ms ease-out";
            card.style.transform = "";
        }
        pointer = null;
    };

    element.addEventListener("pointerdown", onDown);
    element.addEventListener("pointermove", onMove);
    element.addEventListener("pointerup", onUp);
    element.addEventListener("pointercancel", onCancel);

    return {
        dispose() {
            element.removeEventListener("pointerdown", onDown);
            element.removeEventListener("pointermove", onMove);
            element.removeEventListener("pointerup", onUp);
            element.removeEventListener("pointercancel", onCancel);
        },
    };
}
