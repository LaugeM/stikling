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

// A short-lived session token for the API, or null when signed out
export async function getToken() {
    return (await window.Clerk.session?.getToken()) ?? null;
}

export function forget() {
    app = null;
}
