// The share card: one picture of a plant or a propagation, drawn on a canvas and handed to the
// phone's share menu, or saved when the browser can't share a file. The photo is read here and
// never crosses into .NET; C# only sends the words to put on the card.
//
// The card is always light, whatever theme the app is in, because it is posted somewhere else.
// The colours are the light ones from DESIGN.md.

import { getBlob } from "./db.js";
import { download } from "./files.js";

const SIZES = {
    square: { width: 1080, height: 1080, name: 66, latin: 38, line: 40, figure: 300, nameLines: 2, lineLines: 3 },
    portrait: { width: 1080, height: 1350, name: 76, latin: 42, line: 44, figure: 360, nameLines: 2, lineLines: 4 },
    // Stories and TikTok cover the bottom of the picture with their own buttons, so everything
    // that is read stays above the last fifth
    story: { width: 1080, height: 1920, name: 84, latin: 46, line: 48, figure: 420, nameLines: 2, lineLines: 5, safeBottom: 0.8 },
};

const PAPER = "#f6f7f4";
const SURFACE = "#ffffff";
const BORDER = "#dde3dc";
const INK = "#1d2520";
const MUTED = "#5f6b63";
const DEEP_MOSS = "#1f5a37";
const POTTING_GREEN = "#2f7d4f";
const LEAF_TINT = "#e6f2ea";
const CLAY = "#b85c38";
const CLAY_SOFT = "#f6e6dc";

const DISPLAY = '"Bricolage Grotesque", system-ui, sans-serif';
const BODY = 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';

const MARGIN = 72;

// The stroked icons from the app, for a card with neither photo nor number
const ICONS = {
    plant: ["M12 21v-9", "M12 12C12 7 8 4 4 4c0 4 3 8 8 8Z", "M12 14c0-4 3-7 8-7 0 4-3 7-8 7Z"],
    propagation: ["M8 3h8l-1 6v9a3 3 0 0 1-6 0V9Z", "M12 12v4M12 16l-2 2M12 16l2 2"],
};

let cached = null; // the last photo that was drawn, so changing the words doesn't decode it again

// Draws the card on the canvas. Returns { photo, small } saying whether the photo made it onto the
// card and whether only its small version was on this device, or null when a newer draw has taken
// over before this one finished.
// spec: { format, tone, name, latin, cultivar, line, figure: { value, caption } | null,
//         photoId: string | null, frame: { x, y, zoom } | null }
export async function draw(canvas, spec) {
    const token = canvas.stiklingToken = (canvas.stiklingToken ?? 0) + 1;
    const size = SIZES[spec.format] ?? SIZES.square;

    await loadFonts();
    const photo = spec.photoId ? await readPhoto(spec.photoId) : null;
    const bitmap = photo?.bitmap ?? null;
    if (canvas.stiklingToken !== token)
        return null;

    canvas.width = size.width;
    canvas.height = size.height;
    const ctx = canvas.getContext("2d");
    ctx.textBaseline = "alphabetic";

    if (bitmap)
        paintPhotoCard(ctx, size, spec, bitmap);
    else
        paintTextCard(ctx, size, spec);
    // The picture is made now, so sharing has nothing to wait for while the tap's permission to open
    // the share menu is still valid
    canvas.stiklingBlob = toBlob(canvas);
    return { photo: bitmap !== null, small: photo?.small ?? false };
}

// Whether the browser can share a picture as a file
export function canShare() {
    try {
        const file = new File([new Blob()], "card.jpg", { type: "image/jpeg" });
        return typeof navigator.share === "function" && navigator.canShare?.({ files: [file] }) === true;
    } catch {
        return false;
    }
}

// "shared", "cancelled" (closed the share menu), "saved" (it couldn't share, so it was saved) or "failed"
export async function share(canvas, fileName) {
    const blob = await (canvas.stiklingBlob ?? toBlob(canvas));
    if (!blob)
        return "failed";

    const file = new File([blob], fileName, { type: "image/jpeg" });
    if (canShare()) {
        try {
            await navigator.share({ files: [file] });
            return "shared";
        } catch (error) {
            if (error?.name === "AbortError")
                return "cancelled";
            // Something else went wrong with sharing, so fall back to saving it
        }
    }
    return await keep(blob, fileName);
}

export async function save(canvas, fileName) {
    const blob = await (canvas.stiklingBlob ?? toBlob(canvas));
    return blob ? await keep(blob, fileName) : "failed";
}

async function keep(blob, fileName) {
    download(fileName, new Uint8Array(await blob.arrayBuffer()), "image/jpeg");
    return "saved";
}

function toBlob(canvas) {
    return new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.92));
}

async function loadFonts() {
    try {
        await Promise.all([
            document.fonts.load(`800 100px ${DISPLAY}`),
            document.fonts.load(`700 100px ${DISPLAY}`),
            document.fonts.load(`600 100px ${DISPLAY}`),
        ]);
    } catch {
        // The card still draws, in the fallback face
    }
}

// The full photo, or its thumbnail when the full one isn't on this device yet (small is true then).
// Null when neither is.
async function readPhoto(id) {
    // A small one is read again, since the full photo may have arrived since
    if (cached?.id === id && !cached.small)
        return cached;

    const full = await getBlob(id);
    const blob = full ?? (await getBlob(`${id}:thumb`));
    if (!blob)
        return null;

    try {
        const bitmap = await createImageBitmap(blob);
        cached?.bitmap.close();
        cached = { id, bitmap, small: full === null };
        return cached;
    } catch {
        return null;
    }
}

// ---- The card with a photo: the photo above, a label under it ----

function paintPhotoCard(ctx, size, spec, bitmap) {
    const { width, height } = size;
    const textWidth = width - 2 * MARGIN;
    const blocks = layoutText(ctx, size, spec, textWidth);

    const padTop = 60;
    const padBottom = 52;
    const footer = 32;
    const bandHeight = padTop + blocks.height + 36 + footer + padBottom;
    const bandBottom = Math.round(height * (size.safeBottom ?? 1));
    const bandTop = bandBottom - bandHeight;

    ctx.fillStyle = PAPER;
    ctx.fillRect(0, 0, width, height);

    drawCover(ctx, bitmap, spec.frame, 0, 0, width, bandTop);

    ctx.fillStyle = SURFACE;
    ctx.fillRect(0, bandTop, width, bandHeight);
    if (bandBottom < height) {
        ctx.fillStyle = BORDER;
        ctx.fillRect(0, bandBottom - 2, width, 2);
    }

    paintBlocks(ctx, blocks, MARGIN, bandTop + padTop);
    paintFooter(ctx, width - MARGIN, bandBottom - padBottom, footer);
}

// The photo fills the box the way "framed" photos do on the page: zoomed, then moved so the
// framed point is in the middle as far as the photo's edges allow
function drawCover(ctx, bitmap, frame, x, y, w, h) {
    const aspect = bitmap.width / bitmap.height;
    const zoom = Math.min(4, Math.max(1, frame?.zoom ?? 1));
    const dw = zoom * Math.max(w, h * aspect);
    const dh = zoom * Math.max(w / aspect, h);
    const clamp = (value, min, max) => Math.min(max, Math.max(min, value));
    const dx = clamp(w / 2 - (frame?.x ?? 0.5) * dw, w - dw, 0);
    const dy = clamp(h / 2 - (frame?.y ?? 0.5) * dh, h - dh, 0);

    ctx.save();
    ctx.beginPath();
    ctx.rect(x, y, w, h);
    ctx.clip();
    ctx.imageSmoothingQuality = "high";
    ctx.drawImage(bitmap, x + dx, y + dy, dw, dh);
    ctx.restore();
}

// ---- The card without a photo: the big number and the name ----

function paintTextCard(ctx, size, spec) {
    const { width, height } = size;
    const isPlant = spec.tone === "plant";
    const safeBottom = Math.round(height * (size.safeBottom ?? 1));
    const textWidth = width - 2 * MARGIN;

    ctx.fillStyle = isPlant ? CLAY_SOFT : LEAF_TINT;
    ctx.fillRect(0, 0, width, height);

    // The text sits at the bottom of the safe area, under a hairline
    const footer = 32;
    const footerBaseline = safeBottom - MARGIN;
    const blocks = layoutText(ctx, size, spec, textWidth);
    const blocksTop = footerBaseline - footer - 36 - blocks.height;

    ctx.fillStyle = isPlant ? "#e6cdbd" : "#c9ded0";
    ctx.fillRect(MARGIN, blocksTop - 44, textWidth, 2);
    paintBlocks(ctx, blocks, MARGIN, blocksTop);
    paintFooter(ctx, width - MARGIN, footerBaseline, footer);

    // The number, or the icon when there is none, fills the space above
    const top = MARGIN + 40;
    if (spec.figure) {
        let fontSize = size.figure;
        const text = String(spec.figure.value);
        ctx.fillStyle = isPlant ? CLAY : DEEP_MOSS;
        setFont(ctx, `800 ${fontSize}px ${DISPLAY}`, "-0.02em", "condensed");
        while (ctx.measureText(text).width > textWidth && fontSize > 120) {
            fontSize -= 20;
            setFont(ctx, `800 ${fontSize}px ${DISPLAY}`, "-0.02em", "condensed");
        }
        const ascent = ctx.measureText(text).actualBoundingBoxAscent;
        const baseline = top + ascent;
        ctx.fillText(text, MARGIN, baseline);

        const captionSize = Math.round(fontSize * 0.15);
        setFont(ctx, `600 ${captionSize}px ${DISPLAY}`, "0", "normal");
        ctx.fillStyle = INK;
        ctx.fillText(spec.figure.caption, MARGIN + 6, baseline + captionSize * 1.5);
    } else {
        paintIcon(ctx, ICONS[isPlant ? "plant" : "propagation"], MARGIN, top, size.figure * 0.7, isPlant ? CLAY : POTTING_GREEN);
    }
}

function paintIcon(ctx, paths, x, y, extent, color) {
    const scale = extent / 24;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(scale, scale);
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.5;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    for (const path of paths)
        ctx.stroke(new Path2D(path));
    ctx.restore();
}

// ---- Text ----

function setFont(ctx, font, spacing, stretch) {
    ctx.font = font;
    // Not every browser knows these two; the card is only a little wider without them
    if ("letterSpacing" in ctx) ctx.letterSpacing = spacing;
    if ("fontStretch" in ctx) ctx.fontStretch = stretch;
}

// The name, the botanical name and the line, worked out for the width so the card knows how
// tall the text is before it draws anything
function layoutText(ctx, size, spec, maxWidth) {
    const blocks = [];
    let height = 0;

    setFont(ctx, `700 ${size.name}px ${DISPLAY}`, "-0.02em", "semi-condensed");
    const nameLines = wrap(ctx, spec.name, maxWidth, size.nameLines);
    const nameHeight = nameLines.length * size.name * 1.1;
    blocks.push({ kind: "name", lines: nameLines, size: size.name, lineHeight: size.name * 1.1, top: height });
    height += nameHeight;

    if (spec.latin || spec.cultivar) {
        height += 14;
        blocks.push({ kind: "latin", latin: spec.latin, cultivar: spec.cultivar, size: size.latin, top: height });
        height += size.latin * 1.3;
    }

    if (spec.line) {
        setFont(ctx, `400 ${size.line}px ${BODY}`, "0", "normal");
        const lines = wrap(ctx, spec.line, maxWidth, size.lineLines);
        height += 26;
        blocks.push({ kind: "line", lines, size: size.line, lineHeight: size.line * 1.38, top: height });
        height += lines.length * size.line * 1.38;
    }

    return { items: blocks, height, maxWidth };
}

function paintBlocks(ctx, blocks, x, y) {
    for (const block of blocks.items) {
        if (block.kind === "name") {
            setFont(ctx, `700 ${block.size}px ${DISPLAY}`, "-0.02em", "semi-condensed");
            ctx.fillStyle = INK;
            block.lines.forEach((text, i) =>
                ctx.fillText(text, x, y + block.top + block.size * 0.9 + i * block.lineHeight));
        } else if (block.kind === "latin") {
            paintLatin(ctx, block, x, y + block.top + block.size, blocks.maxWidth);
        } else {
            setFont(ctx, `400 ${block.size}px ${BODY}`, "0", "normal");
            ctx.fillStyle = INK;
            block.lines.forEach((text, i) =>
                ctx.fillText(text, x, y + block.top + block.size * 1.05 + i * block.lineHeight));
        }
    }
}

// Genus and species in italic, the cultivar upright in quotes after them
function paintLatin(ctx, block, x, baseline, maxWidth) {
    const parts = [];
    if (block.latin) parts.push({ text: block.latin, style: "italic" });
    if (block.cultivar) parts.push({ text: `${block.latin ? " " : ""}'${block.cultivar}'`, style: "normal" });

    const measure = fontSize => parts.reduce((sum, part) => {
        setFont(ctx, `${part.style} 400 ${fontSize}px ${BODY}`, "0", "normal");
        return sum + ctx.measureText(part.text).width;
    }, 0);

    let fontSize = block.size;
    while (measure(fontSize) > maxWidth && fontSize > block.size * 0.6)
        fontSize -= 2;

    ctx.fillStyle = MUTED;
    let left = x;
    for (const part of parts) {
        setFont(ctx, `${part.style} 400 ${fontSize}px ${BODY}`, "0", "normal");
        ctx.fillText(part.text, left, baseline);
        left += ctx.measureText(part.text).width;
    }
}

function paintFooter(ctx, right, baseline, fontSize) {
    setFont(ctx, `600 ${fontSize}px ${DISPLAY}`, "0.01em", "normal");
    ctx.fillStyle = MUTED;
    ctx.textAlign = "right";
    ctx.fillText("stikling.app", right, baseline);
    ctx.textAlign = "left";
}

// Breaks text into lines that fit, with an ellipsis when there is more than the lines allow
function wrap(ctx, text, maxWidth, maxLines) {
    const fits = s => ctx.measureText(s).width <= maxWidth;
    const words = text.replace(/\s+/g, " ").trim().split(" ").filter(Boolean);
    const lines = [];
    let current = "";

    for (const word of words) {
        if (current && fits(`${current} ${word}`)) {
            current = `${current} ${word}`;
            continue;
        }
        if (current)
            lines.push(current);
        current = word;
        // A word wider than a whole line is broken where it has to be
        while (!fits(current) && current.length > 1) {
            let cut = current.length - 1;
            while (cut > 1 && !fits(current.slice(0, cut)))
                cut--;
            lines.push(current.slice(0, cut));
            current = current.slice(cut);
        }
    }
    if (current)
        lines.push(current);

    if (lines.length <= maxLines)
        return lines;

    let last = lines[maxLines - 1];
    while (last.length > 1 && !fits(`${last}…`))
        last = last.slice(0, -1);
    return [...lines.slice(0, maxLines - 1), `${last.trimEnd()}…`];
}
