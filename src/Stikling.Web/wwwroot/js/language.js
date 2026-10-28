// The language of the app. Loaded in <head> after theme.js, and read by Program.cs before Blazor
// starts, so the app comes up in the right language from the first render.
// The chosen code ("en", "da" and so on) is kept in localStorage only for that first start. The saved
// settings record is what counts, and the app writes it here once it has started.
(function () {
    const storageKey = "stikling-language";

    function get() {
        try {
            return localStorage.getItem(storageKey) || null;
        } catch {
            return null;
        }
    }

    function apply(code) {
        if (code) document.documentElement.lang = code;
    }

    function set(code) {
        try {
            if (code) localStorage.setItem(storageKey, code);
            else localStorage.removeItem(storageKey);
        } catch { /* storage blocked: the choice just won't be remembered */ }
        apply(code);
    }

    // The languages the browser is set to, most preferred first, like ["da-DK", "en"]
    function browser() {
        try {
            if (navigator.languages && navigator.languages.length) return Array.from(navigator.languages);
            return navigator.language ? [navigator.language] : [];
        } catch {
            return [];
        }
    }

    window.stiklingLanguage = { get, set, browser };
    apply(get());
})();
