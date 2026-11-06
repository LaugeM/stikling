// Names the plant in a photo. The photo is made into a JPEG here and sent on, either to Stikling's
// API or straight to Pl@ntNet with a key the person added in Settings. The image never crosses
// into .NET. Each function returns { status, body }: the HTTP status, 0 when the photo isn't on this
// device, -1 when nothing could be reached and 422 when the file can't be read as an image. The body
// is the text of the answer, kept only for a 200 and for the API's 429, which says whose limit it is.
//
// Nothing here ever returns, throws or logs the address, since a key goes in the address. Errors are
// caught and turned into a status.

import { getBlob } from "./db.js";

const MAX_SIDE = 1600;      // longest side in pixels, never scaled up
const JPEG_QUALITY = 0.9;
const PLANTNET = "https://my-api.plantnet.org/v2/identify/all";

// Pl@ntNet only takes JPEG or PNG, and stored photos are mostly WebP
async function toJpeg(source) {
    let bitmap;
    try {
        // "from-image" applies the camera's rotation info so photos aren't sideways
        bitmap = await createImageBitmap(source, { imageOrientation: "from-image" });
    } catch {
        return null;
    }

    try {
        const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.round(bitmap.width * scale));
        canvas.height = Math.max(1, Math.round(bitmap.height * scale));
        const ctx = canvas.getContext("2d");
        ctx.imageSmoothingQuality = "high";
        // A JPEG has no see-through parts, which would otherwise turn black
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        return await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", JPEG_QUALITY));
    } catch {
        return null;
    } finally {
        bitmap.close();
    }
}

// route is { key, lang } for Pl@ntNet, or { address, token, lang } for the API
async function send(jpeg, route) {
    try {
        const form = new FormData();
        const lang = route.lang === "da" ? "da" : "en";
        let response;

        if (route.key) {
            form.append("images", jpeg, "photo.jpg");
            form.append("organs", "auto");
            const address = `${PLANTNET}?api-key=${encodeURIComponent(route.key)}&lang=${lang}&nb-results=5&include-related-images=false`;
            response = await fetch(address, { method: "POST", body: form });
        } else {
            form.append("image", jpeg, "photo.jpg");
            form.append("lang", lang);
            response = await fetch(route.address, {
                method: "POST",
                headers: { "Authorization": `Bearer ${route.token}` },
                body: form
            });
        }

        const keepBody = response.status === 200 || (!route.key && response.status === 429);
        return { status: response.status, body: keepBody ? await response.text() : null };
    } catch {
        return { status: -1, body: null };
    }
}

async function identify(source, route) {
    const jpeg = await toJpeg(source);
    return jpeg ? await send(jpeg, route) : { status: 422, body: null };
}

// The photo chosen in an <input type="file">. The input is left as it is, so the photo can still be saved from it.
export async function identifyInput(input, route) {
    const file = input.files?.[0];
    return file ? await identify(file, route) : { status: 422, body: null };
}

// A photo kept on this device, at its full size
export async function identifyStored(id, route) {
    const blob = await getBlob(id);
    return blob ? await identify(blob, route) : { status: 0, body: null };
}
