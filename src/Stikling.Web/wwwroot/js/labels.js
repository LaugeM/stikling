// Labels for pots, jars and stakes. Every label is drawn by drawLabel, whether it ends up on the
// Labels page, in a picture for a label maker's app, or on a printed page, so they all look alike.
// The drawing is always black on white, whatever theme the app is in, because that is what prints.
//
// The shape says how big the label is: { widthMm, heightMm, cut, stake, oneLine }. It comes from
// Core (LabelShape), which decides what counts as a stake or as one line.
// A label is { number, name, botanical, dateLine }, the parts are { number, name, botanical, date }.

import { download } from "./files.js";
import { DISPLAY, BODY, loadFonts } from "./sharecard.js";

const INK = "#000000";
const PAPER = "#ffffff";

// Dots per millimetre. A thermal label maker prints 203 dpi, which is 8. The labels on the page
// and on paper are drawn finer so the print is crisp.
const PICTURE_DOTS = 8;
const PAGE_DOTS = 12;

// Same-size runs of text shrink down to this many millimetres before they end with an ellipsis
const MIN_NAME_MM = 2;
const MIN_BOTANICAL_MM = 1.7;
const MIN_DATE_MM = 1.5;

// The height of a capital letter compared to the font size, for the faces used here
const CAP = 0.72;
const LINE = 1.25;

// ---- Drawing ----

// Draws the label on the canvas at the given dots per millimetre
export function drawLabel(canvas, label, parts, shape, dotsPerMm) {
    const mm = dotsPerMm;
    const width = Math.round(shape.widthMm * mm);
    const height = Math.round(shape.heightMm * mm);
    canvas.width = width;
    canvas.height = height;

    const ctx = canvas.getContext("2d");
    ctx.fillStyle = PAPER;
    ctx.fillRect(0, 0, width, height);
    ctx.fillStyle = INK;
    ctx.textBaseline = "alphabetic";
    ctx.textAlign = "left";

    const shown = whatIsShown(label, parts, shape);
    const pad = Math.max(0.8, Math.min(2, Math.min(shape.widthMm, shape.heightMm) * 0.08)) * mm;

    if (shape.stake)
        drawStake(ctx, shown, shape, pad, mm);
    else if (shape.oneLine)
        drawOneLine(ctx, shown, { x: pad, y: pad, w: width - 2 * pad, h: height - 2 * pad }, mm);
    else
        drawFlat(ctx, shown, { x: pad, y: pad, w: width - 2 * pad, h: height - 2 * pad }, mm);

    drawCutLine(ctx, shape, width, height, mm);
}

// What the tick boxes and the room on the label leave to draw
function whatIsShown(label, parts, shape) {
    const roomy = !shape.oneLine;
    return {
        number: parts.number && label.number ? label.number : null,
        name: parts.name && label.name ? label.name : null,
        botanical: roomy && parts.botanical && label.botanical ? label.botanical : null,
        date: roomy && parts.date && label.dateLine ? label.dateLine : null,
    };
}

function font(ctx, style, weight, size, family) {
    ctx.font = `${style} ${weight} ${size}px ${family}`;
}

// Shrinks the text to the width, down to a minimum, then cuts it short with an ellipsis.
// Returns the text to draw and the size it fits at.
function fit(ctx, text, style, weight, family, startPx, minPx, maxWidth) {
    let size = Math.max(startPx, minPx);
    font(ctx, style, weight, size, family);
    while (size > minPx && ctx.measureText(text).width > maxWidth) {
        size = Math.max(minPx, size - 1);
        font(ctx, style, weight, size, family);
    }

    let out = text;
    if (ctx.measureText(out).width > maxWidth) {
        while (out.length > 1 && ctx.measureText(`${out}…`).width > maxWidth)
            out = out.slice(0, -1);
        out = `${out.trimEnd()}…`;
    }
    return { text: out, size };
}

// Tape 9 and 12 mm: the number, then the name, on one line
function drawOneLine(ctx, shown, box, mm) {
    const { number, name } = shown;
    if (!number && !name)
        return;

    const gap = 0.35;
    let size = box.h * 0.85;
    const min = MIN_NAME_MM * mm;
    const total = px => {
        let width = 0;
        if (number) {
            font(ctx, "normal", 800, px, DISPLAY);
            width += ctx.measureText(number).width;
        }
        if (number && name)
            width += gap * px;
        if (name) {
            font(ctx, "normal", 600, px, BODY);
            width += ctx.measureText(name).width;
        }
        return width;
    };
    while (size > min && total(size) > box.w)
        size -= 1;
    size = Math.max(size, min);

    const baseline = box.y + box.h / 2 + (size * CAP) / 2;
    let x = box.x;
    if (number) {
        font(ctx, "normal", 800, size, DISPLAY);
        ctx.fillText(number, x, baseline);
        x += ctx.measureText(number).width + (name ? gap * size : 0);
    }
    if (name) {
        const room = box.x + box.w - x;
        const fitted = fit(ctx, name, "normal", 600, BODY, size, size, room);
        ctx.fillText(fitted.text, x, baseline);
    }
}

// A flat label: the number big on the left, the name, botanical name and date stacked to its right.
// Without a number, the text fills the label.
function drawFlat(ctx, shown, box, mm) {
    const hasText = shown.name || shown.botanical || shown.date;
    if (!shown.number) {
        drawTextBlock(ctx, shown, box, mm, 8 * mm);
        return;
    }

    if (!hasText) {
        // Only the number: as big as the label allows, in the middle
        const size = numberSize(ctx, shown.number, box.w, box.h * 1.1, 2 * mm);
        const width = ctx.measureText(shown.number).width;
        ctx.fillText(shown.number, box.x + (box.w - width) / 2, box.y + box.h / 2 + (size * CAP) / 2);
        return;
    }

    const size = numberSize(ctx, shown.number, box.w * 0.42, box.h * 1.1, 2 * mm);
    const numberWidth = ctx.measureText(shown.number).width;
    ctx.fillText(shown.number, box.x, box.y + box.h / 2 + (size * CAP) / 2);

    const gap = Math.max(1.5 * mm, size * 0.15);
    const left = numberWidth + gap;
    drawTextBlock(ctx, shown, { x: box.x + left, y: box.y, w: box.w - left, h: box.h }, mm, 6 * mm);
}

// Sets the number's font as large as fits the width and the start size, and returns the size
function numberSize(ctx, number, maxWidth, startPx, minPx) {
    let size = startPx;
    font(ctx, "normal", 800, size, DISPLAY);
    while (size > minPx && ctx.measureText(number).width > maxWidth) {
        size -= 1;
        font(ctx, "normal", 800, size, DISPLAY);
    }
    return size;
}

// The name, the botanical name in italics and the date small under them, centred as a block in the box
function drawTextBlock(ctx, shown, box, mm, maxNamePx) {
    const lines = [];
    if (shown.name)
        lines.push({ text: shown.name, weight: 1, style: "normal", face: 600, family: BODY, min: MIN_NAME_MM * mm });
    if (shown.botanical)
        lines.push({ text: shown.botanical, weight: 0.72, style: "italic", face: 400, family: BODY, min: MIN_BOTANICAL_MM * mm });
    if (shown.date)
        lines.push({ text: shown.date, weight: 0.58, style: "normal", face: 400, family: BODY, min: MIN_DATE_MM * mm });
    if (lines.length === 0 || box.w <= 0)
        return;

    const weights = lines.reduce((sum, line) => sum + line.weight * LINE, 0);
    const unit = Math.min(box.h / weights, maxNamePx);

    const fitted = lines.map(line => ({
        ...line,
        ...fit(ctx, line.text, line.style, line.face, line.family, unit * line.weight, line.min, box.w),
    }));
    const used = fitted.reduce((sum, line) => sum + line.size * LINE, 0);

    let top = box.y + Math.max(0, (box.h - used) / 2);
    for (const line of fitted) {
        const band = line.size * LINE;
        font(ctx, line.style, line.face, line.size, line.family);
        ctx.fillText(line.text, box.x, top + band / 2 + (line.size * CAP) / 2);
        top += band;
    }
}

// A stake: the number at the top end, the end that stays above the soil, and the name running down it
function drawStake(ctx, shown, shape, pad, mm) {
    const width = shape.widthMm * mm;
    const height = shape.heightMm * mm;
    const inner = width - 2 * pad;
    const point = shape.cut === "point" ? Math.min(width * 0.7, height * 0.2) : 0;

    let textTop = pad;
    if (shown.number) {
        const size = numberSize(ctx, shown.number, inner, inner * 1.1, 2 * mm);
        const numberWidth = ctx.measureText(shown.number).width;
        const cap = size * CAP;
        ctx.fillText(shown.number, pad + (inner - numberWidth) / 2, pad + cap);
        textTop = pad + cap + 2 * mm;
    }

    const length = height - point - pad - textTop;
    if (length <= 0)
        return;

    // Turned a quarter clockwise: the first line is on the right and the text starts nearest the number
    ctx.save();
    ctx.translate(width - pad, textTop);
    ctx.rotate(Math.PI / 2);
    drawTextBlock(ctx, shown, { x: 0, y: 0, w: length, h: inner }, mm, inner * 0.6);
    ctx.restore();
}

// The line to cut along: a box for tags, a pointed end for a stake
function drawCutLine(ctx, shape, width, height, mm) {
    if (shape.cut !== "box" && shape.cut !== "point")
        return;

    const line = Math.max(1, 0.15 * mm);
    const half = line / 2;
    ctx.save();
    ctx.strokeStyle = INK;
    ctx.lineWidth = line;
    ctx.setLineDash([1.2 * mm, 0.8 * mm]);
    ctx.beginPath();
    if (shape.cut === "box") {
        ctx.rect(half, half, width - line, height - line);
    } else {
        const point = Math.min(width * 0.7, height * 0.2);
        ctx.moveTo(half, half);
        ctx.lineTo(width - half, half);
        ctx.lineTo(width - half, height - point);
        ctx.lineTo(width / 2, height - half);
        ctx.lineTo(half, height - point);
        ctx.closePath();
    }
    ctx.stroke();
    ctx.restore();
}

// ---- On the page ----

// Draws the labels on the page, one canvas in the container for each label and in the same order.
// The same canvases are what gets printed.
export async function render(container, labels, parts, shape) {
    await loadFonts();
    const canvases = container.querySelectorAll("canvas[data-label]");
    labels.forEach((label, index) => {
        if (canvases[index])
            drawLabel(canvases[index], label, parts, shape, PAGE_DOTS);
    });
}

// ---- Sharing and printing ----

// Whether the browser can hand picture files to the phone's share menu. Firefox on Android can't.
export function canShareFiles() {
    try {
        const file = new File([new Blob()], "label.png", { type: "image/png" });
        return typeof navigator.share === "function" && navigator.canShare?.({ files: [file] }) === true;
    } catch {
        return false;
    }
}

// One picture per label, sized in dots for a thermal label maker. All of them go to the share menu in
// one go; a browser that can't share files saves them one by one instead.
// "shared", "cancelled" (closed the share menu), "saved" or "failed"
export async function sharePictures(labels, parts, shape, fileNames) {
    await loadFonts();

    const files = [];
    for (let index = 0; index < labels.length; index++) {
        const canvas = document.createElement("canvas");
        drawLabel(canvas, labels[index], parts, shape, PICTURE_DOTS);
        const blob = await toBlob(canvas);
        if (!blob)
            return "failed";
        files.push(new File([blob], fileNames[index], { type: "image/png" }));
    }

    if (typeof navigator.share === "function" && navigator.canShare?.({ files }) === true) {
        try {
            await navigator.share({ files });
            return "shared";
        } catch (error) {
            if (error?.name === "AbortError")
                return "cancelled";
            // Something else went wrong with sharing, so fall back to saving them
        }
    }

    for (const file of files) {
        download(file.name, new Uint8Array(await file.arrayBuffer()), "image/png");
        // A browser asks before it allows several downloads at once, so give each a moment
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    return "saved";
}

function toBlob(canvas) {
    return new Promise(resolve => canvas.toBlob(resolve, "image/png"));
}

// Opens the print dialog with the page the size the labels need. The rule is added for the print
// and taken away again after it, so it doesn't change how other pages print.
export async function print(pageRule) {
    const style = document.createElement("style");
    style.id = "labels-page-rule";
    style.textContent = pageRule;
    document.head.appendChild(style);

    const remove = () => style.remove();
    window.addEventListener("afterprint", remove, { once: true });

    // Let the page lay out the new size first
    await new Promise(resolve => requestAnimationFrame(() => resolve()));
    window.print();
}
