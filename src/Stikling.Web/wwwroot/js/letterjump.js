// The letter strip down the side of a long plant list. Tapping a letter scrolls to the first plant or
// heading for it, and dragging a finger along the strip moves through the letters live, with a bubble
// showing which one it is on. The strip is made by Blazor: each button says where it goes in
// data-target, and has no target when nothing starts with that letter (it is disabled then).
// Pointer events, so a finger and a mouse work the same.

export function attach(nav, bubble) {
    let sliding = null; // the pointer id while a finger or mouse is dragging along the strip
    let current = null; // the letter button the slide is on

    const buttons = () => [...nav.querySelectorAll("button[data-letter]")];
    const reduced = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const scrollTo = (button, smooth) => {
        document.getElementById(button.dataset.target)?.scrollIntoView({
            block: "start",
            behavior: smooth && !reduced() ? "smooth" : "auto"
        });
    };

    // The button at the height of the finger, or the next one with somewhere to go
    const buttonAt = (y) => {
        const all = buttons();
        let index = all.findIndex(b => y < b.getBoundingClientRect().bottom);
        if (index < 0) {
            index = all.length - 1;
        }
        for (let i = index; i < all.length; i++) {
            if (!all[i].disabled) {
                return all[i];
            }
        }
        return null;
    };

    const slideTo = (event, smooth) => {
        const button = buttonAt(event.clientY);
        if (!button) {
            return;
        }
        const top = nav.getBoundingClientRect().top;
        bubble.textContent = button.dataset.letter;
        bubble.style.top = `${event.clientY - top - bubble.offsetHeight / 2}px`;
        bubble.hidden = false;
        if (button !== current) {
            current = button;
            scrollTo(button, smooth);
        }
    };

    const onDown = (event) => {
        if (sliding !== null || (event.pointerType === "mouse" && event.button !== 0)) {
            return;
        }
        event.preventDefault();
        sliding = event.pointerId;
        current = null;
        nav.setPointerCapture(event.pointerId);
        slideTo(event, true);
    };

    const onMove = (event) => {
        if (event.pointerId === sliding) {
            slideTo(event, false);
        }
    };

    const onEnd = (event) => {
        if (event.pointerId !== sliding) {
            return;
        }
        sliding = null;
        current = null;
        bubble.hidden = true;
        if (nav.hasPointerCapture(event.pointerId)) {
            nav.releasePointerCapture(event.pointerId);
        }
    };

    // A pointer has done its jump on pointerdown. A click with no pointer is the keyboard.
    const onClick = (event) => {
        const button = event.target.closest?.("button[data-letter]");
        if (button && !button.disabled && event.detail === 0) {
            scrollTo(button, true);
        }
    };

    nav.addEventListener("pointerdown", onDown);
    nav.addEventListener("pointermove", onMove);
    nav.addEventListener("pointerup", onEnd);
    nav.addEventListener("pointercancel", onEnd);
    nav.addEventListener("click", onClick);

    return {
        dispose() {
            nav.removeEventListener("pointerdown", onDown);
            nav.removeEventListener("pointermove", onMove);
            nav.removeEventListener("pointerup", onEnd);
            nav.removeEventListener("pointercancel", onEnd);
            nav.removeEventListener("click", onClick);
        }
    };
}
