// Stikling's own camera, for taking a photo at the same angle as an earlier one. The picture is
// shown in a <video>, and a shot is stored straight through photos.js, so the image never reaches C#.

import { saveFile } from "./photos.js";

// The video start() was given and the stream it shows, so the camera can come back when the tab
// does, and is let go of even when the video is already gone from the page
let active = null;
let stream = null;

// Counts the times the camera was stopped, so a start that was still waiting for permission
// when the person closed the camera doesn't turn it on afterwards
let stops = 0;

export async function isAvailable() {
    try {
        if (!navigator.mediaDevices?.enumerateDevices)
            return false;
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices.some(device => device.kind === "videoinput");
    } catch {
        return false;
    }
}

function stopTracks(video) {
    stream?.getTracks().forEach(track => track.stop());
    video?.srcObject?.getTracks?.().forEach(track => track.stop());
    stream = null;
    if (video)
        video.srcObject = null;
}

// Returns false when the camera was stopped while it was waiting for permission
async function open(video) {
    const seen = stops;
    const opened = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: { facingMode: { ideal: "environment" }, width: { ideal: 2560 }, height: { ideal: 1920 } }
    });
    if (seen !== stops) {
        opened.getTracks().forEach(track => track.stop());
        return false;
    }

    stream = opened;
    video.muted = true;
    video.playsInline = true;
    video.srcObject = opened;
    await video.play();
    return true;
}

// The camera is released when the tab is hidden, so it isn't left on in the background
async function onVisibility() {
    if (!active)
        return;
    if (document.hidden) {
        stopTracks(active);
    } else if (!active.srcObject) {
        try {
            await open(active);
        } catch {
            // The person can close the camera and open it again
        }
    }
}

// Returns "ok", "denied", "none" or "failed"
export async function start(video) {
    stop(video);
    try {
        if (!navigator.mediaDevices?.getUserMedia)
            return "none";
        if (!await open(video))
            return "failed";
        active = video;
        document.addEventListener("visibilitychange", onVisibility);
        return "ok";
    } catch (error) {
        stopTracks(video);
        switch (error?.name) {
            case "NotAllowedError":
            case "SecurityError":
                return "denied";
            case "NotFoundError":
            case "OverconstrainedError":
                return "none";
            default:
                return "failed";
        }
    }
}

// Saves the whole current frame as a photo, in the same shape as saveFromInput in photos.js
export async function capture(video) {
    try {
        const width = video.videoWidth;
        const height = video.videoHeight;
        if (!width || !height)
            return { saved: [], failed: 1 };

        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        canvas.getContext("2d").drawImage(video, 0, 0, width, height);

        const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.92));
        canvas.width = canvas.height = 0;
        if (!blob)
            return { saved: [], failed: 1 };

        const file = new File([blob], "camera.jpg", { type: "image/jpeg", lastModified: Date.now() });
        const details = await saveFile(file);
        return details ? { saved: [details], failed: 0 } : { saved: [], failed: 1 };
    } catch {
        return { saved: [], failed: 1 };
    }
}

export function stop(video) {
    stops++;
    document.removeEventListener("visibilitychange", onVisibility);
    active = null;
    stopTracks(video);
}

export function clickInput(id) {
    document.getElementById(id)?.click();
}
