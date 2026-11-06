// Error reports to Sentry, from the browser side of the app. Loaded in <head> after the Sentry bundle
// (lib/sentry) and before Blazor starts, so a failure to start is reported too. The .NET side has its
// own SDK, set up in Program.cs.
// Whether reports are allowed is kept in localStorage, since it has to be known before Blazor starts.
// The saved settings record is what counts, and the app applies it once it has started. Both SDKs check
// this copy when they are about to send, so turning reports off takes effect at once.
// Reports are anonymous: no user, no IP address, no query strings, and nothing the person typed in.
(function () {
    const storageKey = "stikling-error-reports";

    function allowed() {
        try {
            return localStorage.getItem(storageKey) !== "off";
        } catch {
            return true;
        }
    }

    function setAllowed(on) {
        try {
            if (on) localStorage.removeItem(storageKey);
            else localStorage.setItem(storageKey, "off");
        } catch { /* storage blocked: the choice just won't be remembered */ }
    }

    // The version isn't known until the app has started, so it's set afterwards on the running client
    function setRelease(release) {
        try {
            const client = window.Sentry && window.Sentry.getClient && window.Sentry.getClient();
            if (client) client.getOptions().release = release;
        } catch { /* reports just go without a version */ }
    }

    // Settles once reports are set up, or once it's clear they won't be. Reporting depends on fetching the
    // settings first, so the start script waits for this before it reports a failed start
    let markReady;
    const ready = new Promise(resolve => markReady = resolve);

    window.stiklingErrors = { allowed, setAllowed, setRelease, ready };

    // Addresses only hold ids, but a query string or fragment could hold more, so they are cut off
    function strip(url) {
        return typeof url === "string" ? url.split(/[?#]/)[0] : url;
    }

    // The messages of a failed start and of a failed download. They name the file that failed and never hold
    // anything typed in, and they are what tells us why the app didn't start.
    function isStartupMessage(value) {
        if (typeof value !== "string") return false;
        return value.startsWith("download '")
            || value.startsWith("optional download '")
            || value.startsWith("Assert failed: This browser/engine doesn't support")
            || value === "blazor.webassembly.js didn't load"
            || value === "Load failed"
            || value === "Failed to fetch"
            || value === "NetworkError when attempting to fetch resource.";
    }

    // Blazor's "Failed to start platform. Reason: <error>" can carry any error from the app's start, which could
    // quote stored data, so the reason is only kept when it is one of the messages above
    function startupMessage(value) {
        if (isStartupMessage(value)) return value;
        const prefix = "Failed to start platform.";
        if (typeof value !== "string" || !value.startsWith(prefix)) return null;
        const reason = value.slice(prefix.length).replace(/^\s*Reason:\s*/, "").replace(/^\w*Error:\s*/, "");
        return isStartupMessage(reason) ? value : prefix;
    }

    // The same as strip, for every address inside a piece of text
    function stripUrls(text) {
        return text.replace(/https?:\/\/[^\s'"<>]+/g, url => strip(url));
    }

    function beforeSend(event) {
        if (!allowed()) return null;

        delete event.user;
        // An error's message can quote what someone typed, so only its type and where it happened are sent.
        // The exception is the messages of a failed start or download, which are kept without query strings.
        for (const exception of event.exception?.values || []) {
            const value = startupMessage(exception.value);
            if (value) exception.value = stripUrls(value);
            else delete exception.value;
        }
        const startup = window.stiklingStartup;
        if (startup) {
            event.tags = Object.assign({}, event.tags, {
                "app.started": String(!!startup.started),
                "startup.hidden": String(!!startup.hidden),
                "startup.retried": String(!!startup.retried),
                "online": String(!!navigator.onLine),
                "sw.controlled": String(!!(navigator.serviceWorker && navigator.serviceWorker.controller))
            });
        }
        if (event.request) {
            event.request.url = strip(event.request.url);
            delete event.request.query_string;
            delete event.request.cookies;
            if (event.request.headers) {
                for (const name of Object.keys(event.request.headers)) {
                    if (name.toLowerCase() === "referer") event.request.headers[name] = strip(event.request.headers[name]);
                }
            }
        }
        for (const crumb of event.breadcrumbs || []) {
            if (!crumb.data) continue;
            for (const key of ["url", "from", "to"]) {
                if (key in crumb.data) crumb.data[key] = strip(crumb.data[key]);
            }
        }
        return event;
    }

    async function start() {
        const Sentry = window.Sentry;
        if (!Sentry) return;

        const local = location.hostname === "localhost" || location.hostname === "127.0.0.1";
        const file = local ? "appsettings.Development.json" : "appsettings.Production.json";
        let dsn;
        try {
            const response = await fetch(new URL(file, document.baseURI));
            if (!response.ok) return;
            dsn = (await response.json())?.Sentry?.Dsn;
        } catch {
            return;
        }
        if (!dsn) return;

        // Filled in by the deploy, so a report from before the app has started still says which version it was
        const release = document.querySelector('meta[name="stikling-release"]')?.getAttribute("content");

        Sentry.init({
            dsn,
            ...(release ? { release } : {}),
            environment: "production",
            sendDefaultPii: false,
            // Errors from browser extensions and other scripts aren't ours
            allowUrls: [location.origin],
            // No sessions, and no breadcrumbs for clicks, console output or requests, which could
            // hold what someone typed in. Navigation is kept, since it only holds addresses.
            integrations: defaults => defaults
                .filter(integration => integration.name !== "BrowserSession" && integration.name !== "Breadcrumbs")
                .concat(Sentry.breadcrumbsIntegration({ console: false, dom: false, fetch: false, xhr: false, history: true })),
            beforeSend
        });
    }

    start().catch(() => { /* no reports */ }).finally(() => markReady());
})();
