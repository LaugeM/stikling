// The time-lapse: a plant's or propagation's photos as one MP4, made on the device. Each photo
// shows for a moment with its date, laid out like the share card, and fades into the next. The
// photos and the finished video stay in this file; C# only sends the words and the timing
// (TimelapsePlan) and gets back progress and a word for how it went.
//
// Two ways to make the video, picked when someone taps Make video:
// - WebCodecs, through Mediabunny (lib/mediabunny), which is quick and uses the phone's encoder.
// - WebAssembly, through h264-mp4-encoder (lib/h264-mp4-encoder), for browsers without WebCodecs,
//   like Firefox on Android. It is much slower, so it makes fewer frames a second.
// Neither library is loaded until a video is made.

import { getBlob, hasBlob } from "./db.js";
import { download } from "./files.js";
import { SIZES, PILL_INSET, loadFonts, paintPhotoCard, drawCover, paintDatePill } from "./sharecard.js";

const FPS = 30;
// Measured with the Node build of the encoder on a desktop: about 70 ms a frame at 1080x1920 and
// 40 ms at 1080x1080, on its fastest setting. A phone is several times slower, so this path makes
// 15 frames a second. Still fine for a photo that changes every half second.
const FALLBACK_FPS = 15;
const MEDIABUNNY = "../lib/mediabunny/mediabunny.min.mjs";
const H264_SCRIPT = "lib/h264-mp4-encoder/h264-mp4-encoder.web.js";
const PAPER = "#f6f7f4";

const CANCELLED = Symbol("cancelled");
const UNSUPPORTED = Symbol("unsupported");

let preview = null; // { canvas, spec, frames, ids, playing, startedAt, raf, lastKey, dirty }
let job = null; // the video being made: { cancelled, output }
let made = null; // the finished video, as a Blob

// ---- Which photos are on this device ----

// Which of these photos only have the small copy here, and which have nothing at all
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

export function prefersReducedMotion() {
    return window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true;
}

// ---- Drawing a frame ----

// The photos around the one on screen. Only the current and the next are kept as bitmaps, since a
// history can have hundreds of photos.
class Frames {
    constructor(shots) {
        this.shots = shots;
        this.bitmaps = new Map(); // index to ImageBitmap, or null when the photo couldn't be read
        this.loading = new Map();
        this.closed = false;
    }

    get(index) {
        return this.bitmaps.get(index) ?? null;
    }

    load(index) {
        if (index < 0 || index >= this.shots.length || this.bitmaps.has(index))
            return Promise.resolve();
        if (!this.loading.has(index)) {
            this.loading.set(index, readBitmap(this.shots[index].photoId).then(bitmap => {
                this.loading.delete(index);
                if (this.closed)
                    bitmap?.close();
                else
                    this.bitmaps.set(index, bitmap);
            }));
        }
        return this.loading.get(index);
    }

    // Closes the bitmaps of every photo but these
    keepOnly(indices) {
        for (const [index, bitmap] of this.bitmaps) {
            if (!indices.includes(index)) {
                bitmap?.close();
                this.bitmaps.delete(index);
            }
        }
    }

    close() {
        this.closed = true;
        this.keepOnly([]);
    }
}

// The full photo, or the small one when that is all that is here
async function readBitmap(id) {
    try {
        const blob = (await getBlob(id)) ?? (await getBlob(`${id}:thumb`));
        return blob ? await createImageBitmap(blob) : null;
    } catch {
        return null;
    }
}

// Which photo is on screen at this time, which one is fading in over it (-1 when none is) and how far
function mixAt(spec, t) {
    const { shots, crossfade } = spec;
    let index = 0;
    while (index + 1 < shots.length && shots[index + 1].start <= t)
        index++;
    const next = index + 1 < shots.length ? index + 1 : -1;
    const alpha = next >= 0 && crossfade > 0
        ? Math.min(1, Math.max(0, (t - (shots[next].start - crossfade)) / crossfade))
        : 0;
    return { index, next, alpha };
}

// Draws the frame at time t. The photos it needs have to be in frames already.
function drawFrame(ctx, size, spec, frames, t) {
    const { index, next, alpha } = mixAt(spec, t);
    ctx.textBaseline = "alphabetic";
    paintPhotoCard(ctx, size, spec, bandTop => {
        paintShot(ctx, size, spec.shots[index], frames.get(index), bandTop, 1);
        if (alpha > 0)
            paintShot(ctx, size, spec.shots[next], frames.get(next), bandTop, alpha);
    });
}

// A photo and its date, both at the same opacity so they fade together
function paintShot(ctx, size, shot, bitmap, bandTop, alpha) {
    ctx.save();
    ctx.globalAlpha = alpha;
    if (bitmap) {
        drawCover(ctx, bitmap, shot.frame, 0, 0, size.width, bandTop);
    } else {
        ctx.fillStyle = PAPER;
        ctx.fillRect(0, 0, size.width, bandTop);
    }
    // The date is set larger on a story, where the photo is the card's whole width
    const scale = size.height > size.width * 1.5 ? 1.25 : 1;
    paintDatePill(ctx, shot.label, PILL_INSET * scale, bandTop - PILL_INSET * scale, size.width - 2 * PILL_INSET * scale, scale);
    ctx.restore();
}

function setCanvasSize(canvas, size) {
    if (canvas.width === size.width && canvas.height === size.height)
        return false;
    canvas.width = size.width;
    canvas.height = size.height;
    return true;
}

// ---- The preview ----

// Plays the time-lapse on the canvas in a loop, or shows its first frame when playing is false.
// Called again whenever the words, speed, size or photos change, and carries on from where it was.
export async function startPreview(canvas, spec, playing) {
    if (job)
        return;
    await loadFonts();
    if (job)
        return;

    if (!preview || preview.canvas !== canvas) {
        stopPreview();
        preview = { canvas, spec, frames: null, ids: "", playing: false, startedAt: 0, raf: 0, lastKey: null, dirty: true };
    }
    const p = preview;
    p.spec = spec;
    const ids = spec.shots.map(s => s.photoId).join();
    if (ids !== p.ids) {
        p.frames?.close();
        p.frames = new Frames(spec.shots);
        p.ids = ids;
    } else {
        p.frames.shots = spec.shots;
    }
    p.dirty = true;

    if (playing && !p.playing) {
        p.playing = true;
        p.startedAt = performance.now();
        p.raf = requestAnimationFrame(() => tick(p));
    } else if (!playing) {
        p.playing = false;
        cancelAnimationFrame(p.raf);
        renderPreview(p, 0);
    }
}

export function stopPreview() {
    if (!preview)
        return;
    cancelAnimationFrame(preview.raf);
    preview.frames?.close();
    preview = null;
}

function tick(p) {
    if (preview !== p || !p.playing)
        return;
    const t = ((performance.now() - p.startedAt) / 1000) % p.spec.total;
    renderPreview(p, t);
    p.raf = requestAnimationFrame(() => tick(p));
}

function renderPreview(p, t) {
    const { spec, frames, canvas } = p;
    const size = SIZES[spec.format] ?? SIZES.story;
    const { index, next } = mixAt(spec, t);
    // The photo after the last one is the first, since it loops
    const ahead = (index + 1) % spec.shots.length;
    const wanted = [index, next, ahead].filter(i => i >= 0);

    frames.keepOnly(wanted);
    for (const i of wanted)
        if (!frames.bitmaps.has(i))
            frames.load(i).then(() => {
                if (preview === p) {
                    p.dirty = true;
                    if (!p.playing)
                        renderPreview(p, 0);
                }
            });

    if (setCanvasSize(canvas, size))
        p.dirty = true;
    const ctx = canvas.getContext("2d");
    // A frame that looks like the last one isn't drawn again, which keeps a hold cheap
    const key = `${mixKey(spec, t)}|${frames.get(index) ? 1 : 0}${frames.get(next) ? 1 : 0}`;
    if (!p.dirty && key === p.lastKey)
        return;
    p.dirty = false;
    p.lastKey = key;
    drawFrame(ctx, size, spec, frames, t);
}

function mixKey(spec, t) {
    const { index, next, alpha } = mixAt(spec, t);
    return alpha > 0 ? `${index}|${next}|${alpha.toFixed(3)}` : `${index}`;
}

// ---- Making the video ----

// Makes the video from the spec and keeps it here. Reports how far it has got to the .NET object,
// a few times a second. Resolves to "done", "cancelled", "unsupported" or "failed".
// spec: { format, name, latin, cultivar, line, crossfade, total, shots: [{ photoId, frame, label, start, hold }] }
export async function make(spec, progress) {
    stopPreview();
    made = null;
    const mine = job = { cancelled: false, output: null };
    const report = throttled(progress);

    try {
        await loadFonts();
        const size = SIZES[spec.format] ?? SIZES.story;

        const ways = [];
        if (await canUseWebCodecs(size))
            ways.push(encodeWithWebCodecs);
        ways.push(encodeWithWasm);

        let last = null;
        for (const way of ways) {
            try {
                report(0, true);
                made = await way(spec, size, mine, report);
                report(1, true);
                return "done";
            } catch (error) {
                if (error === CANCELLED || mine.cancelled)
                    return "cancelled";
                last = error;
                console.warn("Time-lapse:", error);
            }
        }
        return last === UNSUPPORTED && ways.length === 1 ? "unsupported" : "failed";
    } catch (error) {
        console.warn("Time-lapse:", error);
        return mine.cancelled ? "cancelled" : "failed";
    } finally {
        if (job === mine)
            job = null;
    }
}

export function cancel() {
    if (!job)
        return;
    job.cancelled = true;
    job.output?.cancel().catch(() => { });
}

// Progress goes to .NET at most four times a second
function throttled(progress) {
    let sent = 0;
    return (fraction, force = false) => {
        const now = performance.now();
        if (!force && now - sent < 250)
            return;
        sent = now;
        progress.invokeMethodAsync("Report", fraction).catch(() => { });
    };
}

function frameCount(spec, fps) {
    return Math.max(1, Math.ceil(spec.total * fps));
}

// Draws every frame in turn on a canvas of its own, and calls add for the ones that look different
// from the one before. Photos are read one at a time, so a long history doesn't fill the memory.
async function eachFrame(spec, size, fps, canvas, ctx, active, add, report) {
    const frames = new Frames(spec.shots);
    const count = frameCount(spec, fps);
    let lastKey = null;
    try {
        for (let k = 0; k < count; k++) {
            if (active.cancelled)
                throw CANCELLED;
            const t = k / fps;
            const { index, next } = mixAt(spec, t);
            await Promise.all([frames.load(index), frames.load(next)]);
            frames.keepOnly([index, next]);

            const key = mixKey(spec, t) + `|${frames.get(index) ? 1 : 0}${frames.get(next) ? 1 : 0}`;
            const changed = key !== lastKey;
            if (changed) {
                drawFrame(ctx, size, spec, frames, t);
                lastKey = key;
            }
            await add(k, t, changed);

            report(k / count);
            // Let the page breathe, so progress shows and Cancel can be tapped
            if (k % 4 === 0)
                await new Promise(resolve => setTimeout(resolve));
        }
    } finally {
        frames.close();
    }
}

function makeCanvas(size) {
    const canvas = typeof OffscreenCanvas === "function"
        ? new OffscreenCanvas(size.width, size.height)
        : Object.assign(document.createElement("canvas"), { width: size.width, height: size.height });
    return canvas;
}

// ---- WebCodecs, through Mediabunny ----

// About 8 Mbit/s at 1080x1920, less for the smaller shapes
function bitrateFor(size) {
    return Math.round(8_000_000 * (size.width * size.height) / (1080 * 1920));
}

async function canUseWebCodecs(size) {
    if (typeof VideoEncoder === "undefined")
        return false;
    try {
        const { canEncodeVideo, Quality } = await import(MEDIABUNNY);
        return await canEncodeVideo("avc", {
            width: size.width,
            height: size.height,
            frameRate: FPS,
            quality: new Quality({ bitrate: bitrateFor(size) }),
        });
    } catch {
        return false;
    }
}

async function encodeWithWebCodecs(spec, size, active, report) {
    const { Output, Mp4OutputFormat, BufferTarget, CanvasSource, Quality } = await import(MEDIABUNNY);

    const canvas = makeCanvas(size);
    const ctx = canvas.getContext("2d");
    const output = active.output = new Output({
        format: new Mp4OutputFormat({ fastStart: "in-memory" }),
        target: new BufferTarget(),
    });
    const source = new CanvasSource(canvas, {
        codec: "avc",
        quality: new Quality({ bitrate: bitrateFor(size) }),
        keyFrameInterval: 2,
    });
    output.addVideoTrack(source, { frameRate: FPS });

    try {
        await output.start();
        // A frame that looks like the last one is still added, since the canvas still holds it
        await eachFrame(spec, size, FPS, canvas, ctx, active,
            (k, t) => source.add(t, 1 / FPS), report);
        if (active.cancelled)
            throw CANCELLED;
        await output.finalize();
        return new Blob([output.target.buffer], { type: "video/mp4" });
    } catch (error) {
        if (output.state !== "canceled" && output.state !== "finalized")
            await output.cancel().catch(() => { });
        throw error;
    } finally {
        active.output = null;
    }
}

// ---- WebAssembly, through h264-mp4-encoder ----

let h264Loading = null;

// The encoder is a script that defines a global, HME, so it is added to the page the first time
function loadH264() {
    if (typeof HME !== "undefined")
        return Promise.resolve(HME);
    h264Loading ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = H264_SCRIPT;
        script.onload = () => typeof HME !== "undefined" ? resolve(HME) : reject(UNSUPPORTED);
        script.onerror = () => {
            script.remove();
            h264Loading = null;
            reject(UNSUPPORTED);
        };
        document.head.appendChild(script);
    });
    return h264Loading;
}

async function encodeWithWasm(spec, size, active, report) {
    let encoder;
    try {
        encoder = await (await loadH264()).createH264MP4Encoder();
    } catch {
        // The script or the WebAssembly in it wouldn't load, so this browser has no way left
        throw UNSUPPORTED;
    }
    try {
        encoder.width = size.width;
        encoder.height = size.height;
        encoder.frameRate = FALLBACK_FPS;
        encoder.quantizationParameter = 28;
        encoder.speed = 10; // the fastest
        encoder.groupOfPictures = FALLBACK_FPS * 2;
        encoder.initialize();

        const canvas = makeCanvas(size);
        const ctx = canvas.getContext("2d", { willReadFrequently: true });
        let pixels = null;
        await eachFrame(spec, size, FALLBACK_FPS, canvas, ctx, active, async (k, t, changed) => {
            // Reading the pixels back is slow, so a frame that looks like the last one reuses them
            if (changed || !pixels)
                pixels = ctx.getImageData(0, 0, size.width, size.height).data;
            encoder.addFrameRgba(pixels);
        }, report);
        if (active.cancelled)
            throw CANCELLED;

        encoder.finalize();
        // The file lives in the encoder's memory, so copy it out before that is freed
        const bytes = encoder.FS.readFile(encoder.outputFilename).slice();
        try {
            encoder.FS.unlink(encoder.outputFilename);
        } catch {
            // It is freed with the rest
        }
        return new Blob([bytes], { type: "video/mp4" });
    } finally {
        encoder.delete();
    }
}

// ---- Sharing and saving ----

// Whether the browser can share a video as a file
export function canShare() {
    try {
        const file = new File([], "x.mp4", { type: "video/mp4" });
        return typeof navigator.share === "function" && navigator.canShare?.({ files: [file] }) === true;
    } catch {
        return false;
    }
}

export function hasVideo() {
    return made !== null;
}

// Drops the finished video, when something it was made from has changed
export function discard() {
    made = null;
}

// "shared", "cancelled" (closed the share menu), "saved" (it couldn't share, so it was saved) or "failed"
export async function share(fileName) {
    if (!made)
        return "failed";

    const file = new File([made], fileName, { type: "video/mp4" });
    try {
        if (typeof navigator.share === "function" && navigator.canShare?.({ files: [file] }) === true) {
            await navigator.share({ files: [file] });
            return "shared";
        }
    } catch (error) {
        if (error?.name === "AbortError")
            return "cancelled";
        // Something else went wrong with sharing, so fall back to saving it
    }
    return save(fileName);
}

// "saved" or "failed"
export function save(fileName) {
    if (!made)
        return "failed";
    download(fileName, made, "video/mp4");
    return "saved";
}
