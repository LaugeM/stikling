// Watches the service worker for a newer version of the app. The banner that appears
// is Blazor's; this file only reports when an update is ready and applies it.
//
// The page only reloads once the new worker has taken over. Firefox lets it take over only when
// the old worker has no requests in progress, which can take a while. A reload before that is
// served by the old worker, whose files the new one has already deleted, so it brings back the
// old version and the same banner.

const SKIP_WAITING = "skipWaiting";
const ON_UPDATE_READY = "OnUpdateReady";
// Set while an update is being applied, so a reload before it finishes carries on with it
const PENDING_KEY = "stikling-update-pending";
// Reloads allowed while an update is pending, so a worker that never takes over can't loop the page
const MAX_TRIES = 3;
// How long to wait for the new worker before offering Reload and Later again
const GIVE_UP_AFTER_MS = 30000;

async function registration() {
    return navigator.serviceWorker ? navigator.serviceWorker.getRegistration() : null;
}

async function notify(dotNetRef, updating) {
    try {
        await dotNetRef.invokeMethodAsync(ON_UPDATE_READY, updating);
    } catch {
        // The page moved on; nothing to tell
    }
}

function pendingTries() {
    try {
        return Number(sessionStorage.getItem(PENDING_KEY)) || 0;
    } catch {
        return 0;
    }
}

function setPendingTries(tries) {
    try {
        if (tries) sessionStorage.setItem(PENDING_KEY, String(tries));
        else sessionStorage.removeItem(PENDING_KEY);
    } catch {
        // No session storage: a reload before the switch just shows the banner again
    }
}

// The new worker that hasn't taken over this page yet: one still waiting, or one that is active
// but not yet in control (Firefox holds it in "activating" while the old one is busy)
function incoming(reg) {
    if (reg.waiting) return reg.waiting;
    const controller = navigator.serviceWorker.controller;
    if (reg.active && controller && reg.active !== controller) return reg.active;
    return null;
}

/// Tells .NET when a new version has been downloaded and is waiting.
export async function watch(dotNetRef) {
    const reg = await registration();
    if (!reg) return;

    if (pendingTries()) {
        if (incoming(reg) && pendingTries() < MAX_TRIES) {
            // Reloaded while the update was still switching over: keep "Updating…" up and carry on
            await notify(dotNetRef, true);
            if (!(await apply())) await notify(dotNetRef, false);
            return;
        }
        setPendingTries(0);
    }

    // A worker already waiting means the update arrived before this page loaded
    if (reg.waiting && navigator.serviceWorker.controller) await notify(dotNetRef, false);

    reg.addEventListener("updatefound", () => {
        const worker = reg.installing;
        worker?.addEventListener("statechange", async () => {
            // Without a controller this is the first install, not an update
            if (worker.state === "installed" && navigator.serviceWorker.controller) await notify(dotNetRef, false);
        });
    });

    try {
        await reg.update();
    } catch {
        // Offline: we'll find out about updates next time
    }
}

/// Switches to the new version and reloads once it has taken over. Returns false if it still hasn't
/// after a while, so the bar can offer its buttons again rather than reload into the old version.
export async function apply() {
    const reg = await registration();
    const worker = reg && incoming(reg);
    if (!worker) {
        setPendingTries(0);
        location.reload();
        return true;
    }

    setPendingTries(pendingTries() + 1);
    return new Promise(resolve => {
        let done = false;
        const reload = () => {
            if (done) return;
            done = true;
            location.reload();
            resolve(true);
        };

        navigator.serviceWorker.addEventListener("controllerchange", reload, { once: true });
        // Also reload if the worker finishes activating without this page hearing about the switch,
        // or is replaced by an even newer one
        worker.addEventListener("statechange", () => {
            if (worker.state === "activated" || worker.state === "redundant") reload();
        });
        setTimeout(() => {
            if (done) return;
            done = true;
            setPendingTries(0);
            resolve(false);
        }, GIVE_UP_AFTER_MS);

        if (worker.state === "activated") reload();
        else if (worker === reg.waiting) worker.postMessage(SKIP_WAITING);
    });
}
