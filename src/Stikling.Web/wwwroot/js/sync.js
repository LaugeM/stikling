// The browser's moments to sync: the connection coming back, switching back to the app, and
// every few minutes while it is on screen. SyncRunner is the only caller.

const EVERY = 5 * 60 * 1000;

let runner = null;
let timer = null;

function nudge() {
    if (document.visibilityState === "visible") runner?.invokeMethodAsync("OnBrowserNudge");
}

export function watch(dotnet) {
    runner = dotnet;
    if (timer !== null) return;

    window.addEventListener("online", nudge);
    document.addEventListener("visibilitychange", nudge);
    timer = setInterval(nudge, EVERY);
}

export function forget() {
    runner = null;
}
