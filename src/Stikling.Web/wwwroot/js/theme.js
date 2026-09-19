// Light/dark theme handling. Loaded in <head> before Blazor starts, so the right
// theme is applied on the first paint (no white flash in dark mode).
// The chosen mode ("system", "light" or "dark") is remembered in localStorage.
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

    window.stiklingTheme = { getMode, setMode, current: () => resolve(getMode()) };
    apply();
})();
