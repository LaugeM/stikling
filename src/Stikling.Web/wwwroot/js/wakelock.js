// Keeps the screen from locking while someone goes round their plants. Pages call hold() when they open
// and release() when they close. A count of holders means two on one page work, and the lock is let go
// of when the last one releases. The browser drops the lock when the page is hidden, so it is asked for
// again when the page comes back. Where the API is missing, or the browser says no, nothing happens.

let holders = 0;
let lock = null;
let asking = false;

async function acquire() {
    if (asking || lock || holders <= 0 || document.visibilityState !== "visible") {
        return;
    }
    try {
        if (!navigator.wakeLock) {
            return;
        }
        asking = true;
        const sentinel = await navigator.wakeLock.request("screen");
        if (holders <= 0) {
            await sentinel.release();
            return;
        }
        lock = sentinel;
        sentinel.addEventListener("release", () => {
            if (lock === sentinel) {
                lock = null;
            }
        });
    } catch {
        // Not allowed right now, e.g. the battery is low: the screen just locks as usual
    } finally {
        asking = false;
    }
}

document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") {
        acquire();
    }
});

export async function hold() {
    holders++;
    await acquire();
}

export async function release() {
    holders = Math.max(0, holders - 1);
    if (holders === 0 && lock) {
        const sentinel = lock;
        lock = null;
        try {
            await sentinel.release();
        } catch {
            // Already let go of
        }
    }
}
