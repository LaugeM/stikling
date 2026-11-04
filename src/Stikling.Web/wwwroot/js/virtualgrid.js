// Works out how many columns of at least a given width fit in a VirtualGrid, and tells the component when it changes.
const gapRem = 0.75;
const watching = new WeakMap();

export function observe(element, dotNetRef, minColumnRem) {
    unobserve(element);
    let last = 0;
    const update = () => {
        const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
        const style = getComputedStyle(element);
        const width = element.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
        const n = Math.max(1, Math.floor((width + gapRem * rem) / ((minColumnRem + gapRem) * rem)));
        if (n !== last) {
            last = n;
            dotNetRef.invokeMethodAsync("SetColumns", n);
        }
    };
    const observer = new ResizeObserver(update);
    observer.observe(element);
    watching.set(element, observer);
}

export function unobserve(element) {
    const observer = watching.get(element);
    if (observer) {
        observer.disconnect();
        watching.delete(element);
    }
}
