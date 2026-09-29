// The number on the installed app's icon, where the browser supports it.

export async function set(count) {
    if (count > 0 && "setAppBadge" in navigator) await navigator.setAppBadge(count);
    else if ("clearAppBadge" in navigator) await navigator.clearAppBadge();
}
