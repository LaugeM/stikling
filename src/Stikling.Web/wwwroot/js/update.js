// Watches the service worker for a newer version of the app. The banner that appears
// is Blazor's; this file only reports when an update is ready and applies it.

const SKIP_WAITING = "skipWaiting";
const ON_UPDATE_READY = "OnUpdateReady";

async function registration() {
    return navigator.serviceWorker ? navigator.serviceWorker.getRegistration() : null;
}

async function notify(dotNetRef) {
    try {
        await dotNetRef.invokeMethodAsync(ON_UPDATE_READY);
    } catch {
        // The page moved on; nothing to tell
    }
}

/// Tells .NET when a new version has been downloaded and is waiting.
export async function watch(dotNetRef) {
    const reg = await registration();
    if (!reg) return;

    // A worker already waiting means the update arrived before this page loaded
    if (reg.waiting && navigator.serviceWorker.controller) await notify(dotNetRef);

    reg.addEventListener("updatefound", () => {
        const worker = reg.installing;
        worker?.addEventListener("statechange", async () => {
            // Without a controller this is the first install, not an update
            if (worker.state === "installed" && navigator.serviceWorker.controller) await notify(dotNetRef);
        });
    });

    try {
        await reg.update();
    } catch {
        // Offline: we'll find out about updates next time
    }
}

/// Switches to the new version and reloads.
export async function apply() {
    const reg = await registration();
    if (!reg?.waiting) {
        location.reload();
        return;
    }
    navigator.serviceWorker.addEventListener("controllerchange", () => location.reload(), { once: true });
    reg.waiting.postMessage(SKIP_WAITING);
}
