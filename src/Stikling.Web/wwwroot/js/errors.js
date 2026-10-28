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

    window.stiklingErrors = { allowed, setAllowed, setRelease };

    // Addresses only hold ids, but a query string or fragment could hold more, so they are cut off
    function strip(url) {
        return typeof url === "string" ? url.split(/[?#]/)[0] : url;
    }

    function beforeSend(event) {
        if (!allowed()) return null;

        delete event.user;
        // An error's message can quote what someone typed, so only its type and where it happened are sent
        for (const exception of event.exception?.values || []) delete exception.value;
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

        Sentry.init({
            dsn,
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

    start();
})();
