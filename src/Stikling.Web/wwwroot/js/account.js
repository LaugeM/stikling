// Signing in with Clerk. Clerk has no Blazor library, so this is the one file that talks to it,
// and AccountService is the one class that calls this file.
//
// Clerk's scripts come from the instance's Frontend API and are only loaded when needed, so the
// app still starts without a connection.

let loading = null;
let app = null; // the AccountService that is told about changes and asked to navigate

function addScript(src, attributes = {}) {
    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = src;
        script.async = true;
        script.crossOrigin = "anonymous";
        for (const [name, value] of Object.entries(attributes)) script.setAttribute(name, value);
        script.onload = resolve;
        script.onerror = () => {
            script.remove();
            reject(new Error(`Couldn't load ${src}`));
        };
        document.head.appendChild(script);
    });
}

// Clerk's sign-in follows the app's colours, and switches with them in dark mode
const appearance = {
    variables: {
        colorPrimary: "var(--pa-green)",
        colorPrimaryForeground: "#ffffff",
        colorBackground: "var(--pa-surface)",
        colorForeground: "var(--pa-text)",
        colorMutedForeground: "var(--pa-muted)",
        colorNeutral: "var(--pa-text)",
        colorInput: "var(--pa-surface)",
        colorInputForeground: "var(--pa-text)",
        colorBorder: "var(--pa-border)",
        colorDanger: "var(--pa-alert)",
        colorRing: "var(--pa-green)",
        fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
        fontFamilyButtons: "var(--pa-font-display)",
        fontSize: "0.9375rem",
        borderRadius: "0.6rem",
    },
    // Flat and the width of the page, like the app's own cards
    elements: {
        rootBox: { width: "100%" },
        cardBox: { width: "100%", boxShadow: "none", border: "1px solid var(--pa-border)" },
        card: { boxShadow: "none" },
    },
};

function state() {
    const user = window.Clerk.user;
    return {
        signedIn: !!user,
        email: user?.primaryEmailAddress?.emailAddress ?? null,
    };
}

export function load(frontendApi, publishableKey, dotnet) {
    app = dotnet;
    loading ??= (async () => {
        const base = frontendApi.replace(/\/$/, "");
        await Promise.all([
            addScript(`${base}/npm/@clerk/ui@1/dist/ui.browser.js`),
            addScript(`${base}/npm/@clerk/clerk-js@6/dist/clerk.browser.js`, { "data-clerk-publishable-key": publishableKey }),
        ]);

        await window.Clerk.load({
            ui: { ClerkUI: window.__internal_ClerkUICtor },
            appearance,
            // Clerk moves between pages through Blazor's router, so the app isn't reloaded
            routerPush: url => app.invokeMethodAsync("Navigate", url, false),
            routerReplace: url => app.invokeMethodAsync("Navigate", url, true),
        });

        window.Clerk.addListener(() => app?.invokeMethodAsync("OnChanged", state()), { skipInitialEmit: true });
    })().catch(error => {
        loading = null; // so opening the page again tries again
        throw error;
    });

    return loading.then(state);
}

export function mountSignIn(element, redirectUrl) {
    window.Clerk.mountSignIn(element, {
        withSignUp: true,
        fallbackRedirectUrl: redirectUrl,
        signUpFallbackRedirectUrl: redirectUrl,
    });
}

export function unmountSignIn(element) {
    window.Clerk?.unmountSignIn(element);
}

export async function signOut(redirectUrl) {
    await window.Clerk.signOut({ redirectUrl });
    return state();
}

// Deletes the Clerk user, which also ends the session. The instance has to allow users to delete
// themselves, under the user settings in Clerk's dashboard.
export async function deleteUser() {
    await window.Clerk.user.delete();
    return state();
}

// What Clerk has about the signed-in person, for the download of their data. The Stikling server
// doesn't keep it, so the app adds it to the download. The sessions say which devices are signed
// in, and from where.
export async function profile() {
    const user = window.Clerk.user;
    if (!user) return null;

    const sessions = await user.getSessions().catch(() => []);
    return {
        id: user.id,
        firstName: user.firstName,
        lastName: user.lastName,
        username: user.username,
        imageUrl: user.hasImage ? user.imageUrl : null,
        primaryEmailAddress: user.primaryEmailAddress?.emailAddress ?? null,
        emailAddresses: user.emailAddresses.map(e => ({
            emailAddress: e.emailAddress,
            verified: e.verification?.status === "verified",
        })),
        phoneNumbers: user.phoneNumbers.map(p => p.phoneNumber),
        externalAccounts: user.externalAccounts.map(a => ({
            provider: a.provider,
            emailAddress: a.emailAddress,
            username: a.username,
            firstName: a.firstName,
            lastName: a.lastName,
            imageUrl: a.imageUrl,
        })),
        passwordEnabled: user.passwordEnabled,
        twoFactorEnabled: user.twoFactorEnabled,
        createdAt: user.createdAt,
        updatedAt: user.updatedAt,
        lastSignInAt: user.lastSignInAt,
        sessions: sessions.map(s => ({
            lastActiveAt: s.lastActiveAt,
            expireAt: s.expireAt,
            browser: s.latestActivity?.browserName ?? null,
            browserVersion: s.latestActivity?.browserVersion ?? null,
            deviceType: s.latestActivity?.deviceType ?? null,
            ipAddress: s.latestActivity?.ipAddress ?? null,
            city: s.latestActivity?.city ?? null,
            country: s.latestActivity?.country ?? null,
        })),
    };
}

// Passkeys. createPasskey and deletePasskey return { passkeys, problem }, where problem is null or
// one of "cancelled" (the person closed the browser's prompt), "exists" (this device already has a
// passkey for Stikling), "again" (the person confirmed it's them, and has to tap Add once more),
// "reverify" (Clerk wanted the person to confirm it's them and couldn't ask) or "failed".
// AccountService turns it into PasskeyResult.

export function passkeysSupported() {
    return !!window.PublicKeyCredential;
}

function listPasskeys() {
    return (window.Clerk?.user?.passkeys ?? []).map(p => ({
        id: p.id,
        name: p.name ?? null,
        createdAt: p.createdAt ? new Date(p.createdAt).toISOString() : null,
        lastUsedAt: p.lastUsedAt ? new Date(p.lastUsedAt).toISOString() : null,
    }));
}

export function passkeys() {
    return listPasskeys();
}

function errorCodes(e) {
    return [e?.errors?.[0]?.code, e?.code, e?.name].filter(Boolean).map(String);
}

// Clerk refuses changes to a passkey when the sign-in is older than about ten minutes
function needsReverification(e) {
    return (e?.errors ?? []).some(x => x?.code === "session_reverification_required")
        || e?.code === "session_reverification_required";
}

// Runs the action, and if Clerk asks for the person to confirm it's them, shows Clerk's modal for
// that and runs it once more. This is the same internal call Clerk's React hook useReverification
// makes, since clerk-js has no public one. The script is pinned to major version 6 in load(), so
// it can't change under us without a look.
//
// With retry false it stops after the confirmation instead. Browsers only open their passkey prompt
// right after a tap, and the tap that started this was used up by Clerk's modal.
async function withReverification(action, retry = true) {
    try {
        return await action();
    } catch (e) {
        if (!needsReverification(e)) throw e;
        if (typeof window.Clerk.__internal_openReverification !== "function") {
            const unavailable = new Error("Reverification can't be shown");
            unavailable.reverify = true;
            throw unavailable;
        }

        const confirmed = await new Promise(resolve => {
            window.Clerk.__internal_openReverification({
                afterVerification: () => resolve(true),
                afterVerificationCancelled: () => resolve(false),
            });
        });
        if (!confirmed) {
            const cancelled = new Error("Reverification cancelled");
            cancelled.code = "cancelled";
            throw cancelled;
        }
        if (!retry) {
            const again = new Error("Confirmed, tap again");
            again.again = true;
            throw again;
        }
        return await action();
    }
}

function problemOf(e) {
    if (e?.reverify) return "reverify";
    if (e?.again) return "again";
    const codes = errorCodes(e);
    if (e?.name === "NotAllowedError" || codes.some(c => c.includes("cancelled"))) return "cancelled";
    if (e?.name === "InvalidStateError" || codes.includes("passkey_already_exists")) return "exists";
    console.error("Passkey change failed", e);
    return "failed";
}

async function change(action, retry = true) {
    try {
        await withReverification(action, retry);
        await window.Clerk.user.reload();
        return { passkeys: listPasskeys(), problem: null };
    } catch (e) {
        return { passkeys: listPasskeys(), problem: problemOf(e) };
    }
}

export function createPasskey() {
    return change(() => window.Clerk.user.createPasskey(), false);
}

export function deletePasskey(id) {
    return change(() => {
        const passkey = window.Clerk.user.passkeys?.find(p => p.id === id);
        if (!passkey) throw new Error(`No passkey ${id}`);
        return passkey.delete();
    });
}

// A short-lived session token for the API, or null when signed out
export async function getToken() {
    return (await window.Clerk.session?.getToken()) ?? null;
}

export function forget() {
    app = null;
}
