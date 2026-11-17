// Reads QR codes from the camera for the Scan a label page. The picture comes from camera.js, and each
// code that is read is handed to C#, which says whether it opened something. The browser's own
// BarcodeDetector does the reading where there is one, and the bundled jsQR where there isn't.

import * as camera from "./camera.js";

const READS_PER_SECOND = 6;
const LONG_SIDE = 640;

let timer = null;
let reading = false;
let detector = null;
let canvas = null;
let jsQrLoading = null;

// Counts the times scanning was stopped, so a start that finishes after the page closed does nothing
let stops = 0;

// The browser's detector when it reads QR codes, otherwise null
async function getDetector() {
    if (detector !== null)
        return detector || null;
    try {
        if ("BarcodeDetector" in window) {
            const formats = await window.BarcodeDetector.getSupportedFormats();
            if (formats.includes("qr_code")) {
                detector = new window.BarcodeDetector({ formats: ["qr_code"] });
                return detector;
            }
        }
    } catch {
        // Falls back on jsQR
    }
    detector = false;
    return null;
}

// Only loaded when the browser can't read QR codes itself, so other pages don't pay for it
function loadJsQr() {
    if (self.jsQR)
        return Promise.resolve(self.jsQR);
    jsQrLoading ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = "lib/jsqr/jsQR.js";
        script.onload = () => self.jsQR ? resolve(self.jsQR) : reject(new Error("jsQR missing"));
        script.onerror = () => reject(new Error("jsQR didn't load"));
        document.head.appendChild(script);
    }).catch(error => {
        jsQrLoading = null;
        throw error;
    });
    return jsQrLoading;
}

function readWithJsQr(video, jsQR) {
    const width = video.videoWidth;
    const height = video.videoHeight;
    if (!width || !height)
        return null;

    const scale = Math.min(1, LONG_SIDE / Math.max(width, height));
    const w = Math.max(1, Math.round(width * scale));
    const h = Math.max(1, Math.round(height * scale));
    canvas ??= document.createElement("canvas");
    canvas.width = w;
    canvas.height = h;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    context.drawImage(video, 0, 0, w, h);
    const picture = context.getImageData(0, 0, w, h);
    return jsQR(picture.data, w, h, { inversionAttempts: "dontInvert" })?.data ?? null;
}

// Returns "ok", "denied", "none" or "failed". The target's OnRead(text) returns true when it opened something.
export async function start(video, target) {
    stopReading();
    const seen = stops;

    const native = await getDetector();
    let jsQR = null;
    if (!native) {
        try {
            jsQR = await loadJsQr();
        } catch {
            return "failed";
        }
    }

    const result = await camera.start(video);
    if (result !== "ok")
        return result;
    // The page closed while the camera was starting
    if (seen !== stops) {
        camera.stop(video);
        return "failed";
    }

    timer = setInterval(async () => {
        if (reading || document.hidden || video.readyState < 2)
            return;
        reading = true;
        try {
            let text = null;
            if (native) {
                try {
                    text = (await native.detect(video))[0]?.rawValue ?? null;
                } catch {
                    // A frame that can't be read is skipped
                }
            } else {
                text = readWithJsQr(video, jsQR);
            }
            if (text && timer !== null && seen === stops) {
                if (await target.invokeMethodAsync("OnRead", text)) {
                    stopReading();
                    navigator.vibrate?.(60);
                }
            }
        } catch {
            // The page is going away
        } finally {
            reading = false;
        }
    }, 1000 / READS_PER_SECOND);
    return "ok";
}

function stopReading() {
    if (timer !== null)
        clearInterval(timer);
    timer = null;
}

export function stop(video) {
    stops++;
    stopReading();
    camera.stop(video);
}

// Goes back when there is somewhere to go back to, and says whether it did
export function goBack() {
    if (history.length > 1) {
        history.back();
        return true;
    }
    return false;
}
