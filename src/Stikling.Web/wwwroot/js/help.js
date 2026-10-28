// The search on the Help page. It filters the questions in the page as they are, rather than going
// through Blazor, since the answers are markup in Help.razor and not data. Every word typed has to be in
// the question or its answer. A topic with nothing left is hidden, and when only a few answers match
// they open. Returns something to call dispose() on when the page goes away.
export function attachSearch(input, noMatch) {
    // The page can be left before this runs, and the elements are gone by then
    if (!input || !noMatch)
        return { setValue: () => { }, clear: () => { }, dispose: () => { } };

    const openUpTo = 5;
    const fold = text => text.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
    const toc = document.querySelector(".help-toc");
    const sections = [...document.querySelectorAll(".help-section")].map(section => ({
        section,
        items: [...section.querySelectorAll(".help-item")].map(item => ({ item, text: fold(item.textContent) }))
    }));
    // The ones the search opened, so it closes them again without closing what someone opened by hand
    const opened = new Set();

    const filter = () => {
        const words = fold(input.value).split(/\s+/).filter(word => word);
        const searching = words.length > 0;
        const matches = [];

        for (const { section, items } of sections) {
            const visible = [];
            for (const { item, text } of items) {
                const match = !searching || words.every(word => text.includes(word));
                item.hidden = !match;
                item.classList.remove("help-first", "help-last");
                if (match)
                    visible.push(item);
            }
            section.hidden = visible.length === 0;
            section.classList.toggle("help-searching", searching);
            if (searching && visible.length > 0) {
                visible[0].classList.add("help-first");
                visible[visible.length - 1].classList.add("help-last");
                matches.push(...visible);
            }
        }

        const toOpen = new Set(searching && matches.length <= openUpTo ? matches : []);
        for (const item of opened) {
            if (toOpen.has(item))
                continue;
            item.open = false;
            opened.delete(item);
        }
        for (const item of toOpen) {
            if (item.open)
                continue;
            item.open = true;
            opened.add(item);
        }

        if (toc)
            toc.hidden = searching;
        noMatch.hidden = !searching || matches.length > 0;
    };

    const clear = () => {
        input.value = "";
        filter();
    };

    const onKey = event => {
        if (event.key === "Escape" && input.value)
            clear();
    };

    // On a wide screen the topic list marks the topic in view. A topic counts once its top is in
    // the upper part of the screen, so the last one to get there is the one being read.
    let observer = null;
    if (toc && matchMedia("(min-width: 64rem)").matches) {
        const links = new Map([...toc.querySelectorAll("a")].map(link => [link.hash.slice(1), link]));
        const mark = id => {
            for (const [name, link] of links) {
                link.classList.toggle("current", name === id);
                if (name === id)
                    link.setAttribute("aria-current", "location");
                else
                    link.removeAttribute("aria-current");
            }
        };
        observer = new IntersectionObserver(entries => {
            const entering = entries.filter(entry => entry.isIntersecting);
            if (entering.length > 0)
                mark(entering[entering.length - 1].target.id);
        }, { rootMargin: "0px 0px -75% 0px" });
        for (const { section } of sections)
            observer.observe(section);
    }

    input.addEventListener("input", filter);
    input.addEventListener("keydown", onKey);
    return {
        // Puts a search in the box, like the one typed on the Search page
        setValue: value => {
            input.value = value;
            filter();
        },
        clear: () => {
            clear();
            input.focus();
        },
        dispose: () => {
            observer?.disconnect();
            input.removeEventListener("input", filter);
            input.removeEventListener("keydown", onKey);
        }
    };
}
