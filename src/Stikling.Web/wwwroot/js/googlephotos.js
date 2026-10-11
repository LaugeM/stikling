// Picking photos from Google Photos with Google's Photos Picker API. The app gets a short-lived
// token from Google, makes a picking session, the person picks in Google Photos, and the picked
// photos are downloaded one at a time and saved with saveFile from photos.js. Nothing goes
// through Stikling's API.
//
// The token is only held in a variable here, with its expiry. It is never stored and never
// passed to .NET, so closing the page drops it.

import { saveFile } from "./photos.js";

const SCOPE = "https://www.googleapis.com/auth/photospicker.mediaitems.readonly";
const API = "https://photospicker.googleapis.com/v1/";
const DEFAULT_POLL_MS = 5000;
const DEFAULT_TIMEOUT_MS = 1800 * 1000;

let token = null;
let expiresAt = 0;
let granted = false; // a token has been given in this page, so Google needn't ask again
let session = null;  // { id, pollMs, deadline }
let cancelled = false;
let wake = null;
let loading = null;

const hasGoogle = () => !!window.google?.accounts?.oauth2;

// Loads Google's sign-in script once. Resolves true when it is ready, false when it couldn't load.
export function prepare() {
    if (hasGoogle()) return Promise.resolve(true);
    if (loading) return loading;

    loading = new Promise(resolve => {
        const script = document.createElement("script");
        script.src = "https://accounts.google.com/gsi/client";
        script.async = true;
        script.onload = () => resolve(hasGoogle());
        script.onerror = () => {
            script.remove();
            loading = null; // allow another try
            resolve(false);
        };
        document.head.appendChild(script);
    });
    return loading;
}

// A session can wait 30 minutes for picks and the downloads come after, so a token is only
// reused while it has more than that left
const TOKEN_MARGIN_MS = 35 * 60 * 1000;

// Resolves "ok", "denied" or "failed". The popup has to open straight from the click, so this
// must be reached before anything slow is awaited.
function getToken(clientId) {
    if (token && expiresAt - Date.now() > TOKEN_MARGIN_MS) return Promise.resolve("ok");
    if (!hasGoogle()) return Promise.resolve("failed");

    return new Promise(resolve => {
        try {
            const client = google.accounts.oauth2.initTokenClient({
                client_id: clientId,
                scope: SCOPE,
                callback: response => {
                    if (response.error || !response.access_token) {
                        resolve("denied");
                        return;
                    }
                    token = response.access_token;
                    expiresAt = Date.now() + Number(response.expires_in || 3600) * 1000;
                    granted = true;
                    resolve("ok");
                },
                error_callback: error => resolve(error?.type === "popup_closed" ? "denied" : "failed")
            });
            client.requestAccessToken(granted ? { prompt: "" } : {});
        } catch {
            resolve("failed");
        }
    });
}

function call(path, init = {}) {
    return fetch(API + path, { ...init, headers: { ...init.headers, "Authorization": `Bearer ${token}` } });
}

// "5s" or "1799.9s" as milliseconds
const seconds = text => {
    const value = parseFloat(text);
    return Number.isFinite(value) ? value * 1000 : null;
};

export async function start(clientId, maxItems) {
    cancelled = false;

    // No await before this, so the browser lets Google's popup open
    const result = await getToken(clientId);
    if (result !== "ok") return { status: result };
    if (cancelled) return { status: "cancelled" };

    try {
        const response = await call("sessions", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ pickingConfig: { maxItemCount: String(maxItems) } })
        });
        if (!response.ok) {
            if (response.status === 401 || response.status === 403) token = null;
            return { status: "failed" };
        }

        const created = await response.json();
        if (cancelled) {
            call(`sessions/${created.id}`, { method: "DELETE" }).catch(() => { });
            return { status: "cancelled" };
        }
        session = {
            id: created.id,
            pollMs: seconds(created.pollingConfig?.pollInterval) ?? DEFAULT_POLL_MS,
            deadline: Date.now() + (seconds(created.pollingConfig?.timeoutIn) ?? DEFAULT_TIMEOUT_MS)
        };
        return { status: "ready", pickerUri: created.pickerUri + "/autoclose" };
    } catch {
        return { status: "failed" };
    }
}

// Waits a while, or less when the page comes back to the front or the wait is cancelled
function pause(ms) {
    return new Promise(resolve => {
        const timer = setTimeout(done, ms);
        function done() {
            clearTimeout(timer);
            document.removeEventListener("visibilitychange", onVisible);
            wake = null;
            resolve();
        }
        function onVisible() {
            if (document.visibilityState === "visible") done();
        }
        document.addEventListener("visibilitychange", onVisible);
        wake = done;
    });
}

async function endSession() {
    const current = session;
    session = null;
    if (!current || !token) return;
    try {
        await call(`sessions/${current.id}`, { method: "DELETE" });
    } catch {
        // Google drops the session by itself after a while
    }
}

async function listItems(id) {
    const items = [];
    let pageToken = "";
    do {
        const query = `mediaItems?sessionId=${encodeURIComponent(id)}&pageSize=100`
            + (pageToken ? `&pageToken=${encodeURIComponent(pageToken)}` : "");
        const response = await call(query);
        if (!response.ok) throw new Error("list");
        const page = await response.json();
        items.push(...(page.mediaItems ?? []));
        pageToken = page.nextPageToken ?? "";
    } while (pageToken);
    return items;
}

// Polls until the person has picked, then downloads and saves each photo. Progress(n, total)
// is called on dotnetRef before each one.
export async function wait(dotnetRef) {
    const current = session;
    if (!current) return { status: "timedOut" };

    try {
        let items = null;

        while (items === null) {
            if (cancelled) return { status: "cancelled" };
            if (Date.now() > current.deadline) return { status: "timedOut" };

            try {
                const response = await call(`sessions/${current.id}`);
                if (cancelled) return { status: "cancelled" };
                if (response.status === 404) return { status: "timedOut" };
                if (response.status === 401 || response.status === 403) return { status: "failed" };

                if (response.ok) {
                    const state = await response.json();
                    if (state.pollingConfig?.timeoutIn === "0s") return { status: "timedOut" };
                    current.pollMs = seconds(state.pollingConfig?.pollInterval) ?? current.pollMs;
                    if (state.mediaItemsSet) items = await listItems(current.id);
                }
            } catch {
                // A network error in one poll is tried again at the next one
            }

            if (items === null) await pause(current.pollMs);
        }

        return await fetchAll(items, dotnetRef, () => cancelled);
    } finally {
        await endSession();
    }
}

async function fetchAll(items, dotnetRef, isCancelled) {
    const photos = items.filter(item => item.type === "PHOTO" && item.mediaFile?.baseUrl);
    const saved = [];
    let failed = items.length - photos.length;
    let networkFailed = false;

    for (let i = 0; i < photos.length; i++) {
        if (isCancelled()) break;

        try {
            await dotnetRef.invokeMethodAsync("Progress", i + 1, photos.length);
        } catch {
            // Progress is only for show
        }

        const item = photos[i];
        let details = null;
        try {
            // The photo itself is on Google's image host, not under the API address
            const response = await fetch(item.mediaFile.baseUrl + "=d", { headers: { "Authorization": `Bearer ${token}` } });
            if (response.ok) {
                const blob = await response.blob();
                const file = new File([blob], item.mediaFile.filename ?? "photo.jpg", {
                    type: blob.type || item.mediaFile.mimeType,
                    lastModified: Date.parse(item.createTime) || Date.now()
                });
                details = await saveFile(file);
            }
        } catch {
            networkFailed = true;
        }

        if (details) saved.push(details);
        else failed++;
    }

    // Photos already saved are kept, so a cancel part way through still returns them
    if (isCancelled() && saved.length === 0) return { status: "cancelled" };
    if (saved.length === 0 && networkFailed) return { status: "failed" };
    return { status: "done", saved, failed };
}

export function cancel() {
    cancelled = true;
    wake?.();
    // A running wait ends the session itself once it has stopped
    if (!wake && session) endSession();
}
