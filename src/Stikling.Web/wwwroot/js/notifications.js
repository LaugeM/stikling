// Web Push for the daily notification: whether this browser can, the permission, and the
// subscription the server sends to. The permission is only ever asked for in subscribe(), which
// runs from the Turn on tap.

// "ok", "needs-home-screen" (iPhone and iPad, until the app is added to the home screen) or "unsupported"
export function support() {
    const apple = /iPhone|iPad|iPod/.test(navigator.userAgent)
        || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
    const standalone = navigator.standalone === true || matchMedia("(display-mode: standalone)").matches;
    const canPush = "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;

    if (canPush) return "ok";
    if (apple && !standalone) return "needs-home-screen";
    return "unsupported";
}

// "granted", "denied" or "default"
export function permission() {
    return "Notification" in window ? Notification.permission : "denied";
}

// The IANA name of the time zone, like "Europe/Copenhagen"
export function timeZone() {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

// Asks for permission and subscribes. Gives { endpoint, p256dh, auth }, or { reason } when it
// can't: "denied" or "failed".
export async function subscribe(publicKey) {
    try {
        if (await Notification.requestPermission() !== "granted") return { reason: "denied" };

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: fromBase64Url(publicKey),
        });
        return describe(subscription);
    } catch {
        return { reason: "failed" };
    }
}

// Removes the subscription from the browser. True when there was one.
export async function unsubscribe() {
    try {
        const subscription = await current();
        return subscription ? await subscription.unsubscribe() : false;
    } catch {
        return false;
    }
}

// The subscription the browser has now, or null
export async function currentSubscription() {
    try {
        const subscription = await current();
        return subscription ? describe(subscription) : null;
    } catch {
        return null;
    }
}

async function current() {
    if (!("serviceWorker" in navigator) || !("PushManager" in window)) return null;
    const registration = await navigator.serviceWorker.ready;
    return await registration.pushManager.getSubscription();
}

function describe(subscription) {
    return {
        endpoint: subscription.endpoint,
        p256dh: toBase64Url(subscription.getKey("p256dh")),
        auth: toBase64Url(subscription.getKey("auth")),
        reason: null,
    };
}

function toBase64Url(buffer) {
    if (!buffer) return "";
    let text = "";
    for (const byte of new Uint8Array(buffer)) text += String.fromCharCode(byte);
    return btoa(text).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function fromBase64Url(text) {
    const padded = (text + "=".repeat((4 - text.length % 4) % 4)).replace(/-/g, "+").replace(/_/g, "/");
    return Uint8Array.from(atob(padded), c => c.charCodeAt(0));
}
