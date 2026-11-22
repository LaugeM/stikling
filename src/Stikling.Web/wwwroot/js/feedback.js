// The browser and system for the Write to me sheet, like "Firefox 133 on Android". Only the name and
// the main version, so it says what is needed to reproduce a problem and nothing that picks one person out.
export function browserText() {
    const data = navigator.userAgentData;
    const agent = navigator.userAgent || "";

    let browser = null;
    let system = data?.platform || null;

    if (data?.brands?.length) {
        const brands = data.brands.filter(b => !/not.?a.?brand/i.test(b.brand));
        const named = brands.find(b => !/^chromium$/i.test(b.brand)) ?? brands[0];
        if (named)
            browser = `${named.brand.replace(/^Google /, "")} ${named.version}`;
    }

    if (!browser) {
        const patterns = [
            [/EdgA?\/(\d+)/, "Edge"],
            [/OPR\/(\d+)/, "Opera"],
            [/SamsungBrowser\/(\d+)/, "Samsung Internet"],
            [/(?:Firefox|FxiOS)\/(\d+)/, "Firefox"],
            [/(?:Chrome|CriOS)\/(\d+)/, "Chrome"],
            [/Version\/(\d+).*Safari/, "Safari"]
        ];
        for (const [pattern, name] of patterns) {
            const match = agent.match(pattern);
            if (match) {
                browser = `${name} ${match[1]}`;
                break;
            }
        }
    }

    if (!system) {
        const systems = [
            [/Android/, "Android"],
            [/iPhone|iPad|iPod/, "iOS"],
            [/Windows/, "Windows"],
            [/Mac OS X|Macintosh/, "macOS"],
            [/CrOS/, "ChromeOS"],
            [/Linux/, "Linux"]
        ];
        system = systems.find(([pattern]) => pattern.test(agent))?.[1] ?? null;
    }

    if (browser && system)
        return `${browser} on ${system}`;
    return browser ?? system ?? "unknown browser";
}
