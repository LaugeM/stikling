// The language of the app. Loaded in <head> after theme.js, and read by Program.cs before Blazor
// starts, so the app comes up in the right language from the first render.
// The chosen code ("en", "da" and so on) is kept in localStorage only for that first start. The saved
// settings record is what counts, and the app writes it here once it has started.
// The live site also has a copy of each page for every ready language under /<code>/, like /da/help, for search
// engines and for links in that language (see .github/scripts/prepare-pages.py). The app has no such routes, so this
// takes the code off the address and remembers it as the language of the page the person arrived on. It only
// applies when no language has been chosen, and isn't saved as a choice. The codes come from the hreflang links
// in <head>, which only the live site has, so nothing changes on localhost.
(function () {
    const storageKey = "stikling-language";

    function get() {
        try {
            return localStorage.getItem(storageKey) || null;
        } catch {
            return null;
        }
    }

    // The language part of a code, like "da" for "da-DK"
    function primary(code) {
        return String(code || "").split("-")[0].toLowerCase();
    }

    // The languages the browser is set to, most preferred first, as the browser reports them
    function browserList() {
        try {
            return navigator.languages && navigator.languages.length
                ? Array.from(navigator.languages)
                : navigator.language ? [navigator.language] : [];
        } catch {
            return [];
        }
    }

    // Browser translation (Chrome's, for one) rewrites the page's text, which breaks Blazor's updates to it, and
    // makes nonsense of text already in the reader's language ("I dag" becomes "Jeg dag"). So when the app is in a
    // language the person chose or their browser is set to, the browser is told not to translate it. When the app is
    // in English only because it lacks their language, translation is left on for them to use.
    function protect(code) {
        const root = document.documentElement;
        const theirs = !!code && (get() === code || browserList().some(l => primary(l) === primary(code)));
        if (theirs) {
            root.setAttribute("translate", "no");
            root.classList.add("notranslate");
        } else {
            root.removeAttribute("translate");
            root.classList.remove("notranslate");
        }
    }

    function apply(code) {
        if (code) document.documentElement.lang = code;
        protect(code || document.documentElement.lang);
    }

    function set(code) {
        try {
            if (code) localStorage.setItem(storageKey, code);
            else localStorage.removeItem(storageKey);
        } catch { /* storage blocked: the choice just won't be remembered */ }
        apply(code);
    }

    // The language code in the address, like "da" for /da/help, taken off so the app sees /help
    function fromAddress() {
        try {
            const codes = Array.from(document.querySelectorAll('link[rel="alternate"][hreflang]'))
                .map(link => (link.getAttribute("hreflang") || "").toLowerCase())
                .filter(code => code && code !== "x-default" && code !== "en");
            if (!codes.length) return null;

            const base = new URL(document.baseURI).pathname;
            if (!location.pathname.startsWith(base)) return null;
            const rest = location.pathname.slice(base.length);
            const code = rest.split("/")[0].toLowerCase();
            if (!codes.includes(code)) return null;

            const path = base + rest.slice(code.length).replace(/^\//, "");
            history.replaceState(history.state, "", path + location.search + location.hash);
            return code;
        } catch {
            return null;
        }
    }

    const pageLanguage = fromAddress();

    // The language of the page the person arrived on, like "da" after opening /da/, or null
    function page() {
        return pageLanguage;
    }

    // The languages the browser is set to, most preferred first, like ["da-DK", "en"]. With no language chosen,
    // the one in the address comes first
    function browser() {
        try {
            const list = navigator.languages && navigator.languages.length
                ? Array.from(navigator.languages)
                : navigator.language ? [navigator.language] : [];
            return pageLanguage && !get() ? [pageLanguage, ...list] : list;
        } catch {
            return pageLanguage && !get() ? [pageLanguage] : [];
        }
    }

    // The static pages have a hidden bar for each other language, like "Se siden på dansk". With no language chosen,
    // the one for the first language in the browser's list that has a bar is shown, unless that list starts with the
    // page's own language. Nothing is stored and nothing redirects.
    function suggest() {
        try {
            if (get()) return;
            const list = browserList().map(primary);
            if (!list.length || list[0] === (pageLanguage || "en")) return;
            const bars = Array.from(document.querySelectorAll(".language-suggest[data-lang]"));
            for (const code of list) {
                const bar = bars.find(b => primary(b.getAttribute("data-lang")) === code);
                if (bar) {
                    bar.hidden = false;
                    return;
                }
            }
        } catch { /* the bar just stays hidden */ }
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", suggest);
    else suggest();

    window.stiklingLanguage = { get, set, browser, page, show: apply };
    apply(get());
})();
