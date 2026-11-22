// Light/dark theme handling. Loaded in <head> before Blazor starts, so the right
// theme is applied on the first paint (no white flash in dark mode).
// The chosen mode ("system", "light" or "dark") is kept in localStorage only for that first
// paint. The saved settings record is what counts, and the app applies it once it has started.
(function () {
    const storageKey = "stikling-theme";
    const media = window.matchMedia("(prefers-color-scheme: dark)");

    function getMode() {
        try {
            const mode = localStorage.getItem(storageKey);
            return mode === "light" || mode === "dark" ? mode : "system";
        } catch {
            return "system";
        }
    }

    function resolve(mode) {
        return mode === "system" ? (media.matches ? "dark" : "light") : mode;
    }

    function apply() {
        const theme = resolve(getMode());
        // Bootstrap 5.3 switches all its colours on this attribute
        document.documentElement.setAttribute("data-bs-theme", theme);
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute("content", theme === "dark" ? "#121814" : "#2f7d4f");
        return theme;
    }

    function setMode(mode) {
        try {
            if (mode === "system") localStorage.removeItem(storageKey);
            else localStorage.setItem(storageKey, mode);
        } catch { /* storage blocked: the choice just won't be remembered */ }
        return apply();
    }

    // Follow the OS setting live while in "system" mode
    media.addEventListener("change", () => { if (getMode() === "system") apply(); });

    // The look: text size, contrast, motion and large buttons. The person's own switches are kept
    // in localStorage as {"text","contrast","motion","large"} for the first paint, the same way as the
    // theme. The saved settings are what count, and the app applies them once it has started.
    const lookKey = "stikling-look";
    const contrastMedia = window.matchMedia("(prefers-contrast: more)");
    const motionMedia = window.matchMedia("(prefers-reduced-motion: reduce)");

    function getLook() {
        let stored = null;
        try {
            stored = JSON.parse(localStorage.getItem(lookKey));
        } catch { /* nothing stored, or storage blocked */ }
        const look = stored && typeof stored === "object" ? stored : {};
        return {
            text: look.text === "larger" || look.text === "largest" ? look.text : "normal",
            contrast: look.contrast === true,
            motion: look.motion === true,
            large: look.large === true
        };
    }

    function applyLook() {
        const look = getLook();
        const root = document.documentElement;
        const set = (name, value) => value === null ? root.removeAttribute(name) : root.setAttribute(name, value);
        set("data-text-size", look.text === "normal" ? null : look.text);
        set("data-contrast", look.contrast || contrastMedia.matches ? "more" : null);
        set("data-motion", look.motion || motionMedia.matches ? "reduce" : null);
        set("data-large-buttons", look.large ? "" : null);
    }

    function setLook(look) {
        try {
            const value = {
                text: look?.text === "larger" || look?.text === "largest" ? look.text : "normal",
                contrast: look?.contrast === true,
                motion: look?.motion === true,
                large: look?.large === true
            };
            if (value.text === "normal" && !value.contrast && !value.motion && !value.large) localStorage.removeItem(lookKey);
            else localStorage.setItem(lookKey, JSON.stringify(value));
        } catch { /* storage blocked: the choice just won't be remembered */ }
        applyLook();
    }

    // True when the switch is on or the phone asks for less motion. Scripts that move things on
    // screen ask this instead of the media query, so the switch counts too.
    const reducedMotion = () => getLook().motion || motionMedia.matches;

    contrastMedia.addEventListener("change", applyLook);
    motionMedia.addEventListener("change", applyLook);

    window.stiklingTheme = { getMode, setMode, current: () => resolve(getMode()), getLook, setLook, reducedMotion };
    apply();
    applyLook();
})();
