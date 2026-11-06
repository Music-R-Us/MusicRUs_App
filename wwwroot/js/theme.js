// Restores the saved theme on startup and exposes theme helpers to Blazor.
window.MusicRUsTheme = (() => {
    const storageKey = "musicrus-theme";

    // Reads the currently applied theme, defaulting to dark.
    function getTheme() {
        return document.documentElement.dataset.theme === "light"
            ? "light"
            : "dark";
    }

    // Applies the theme immediately and remembers it for future visits.
    function setTheme(theme) {
        const selectedTheme = theme === "light" ? "light" : "dark";

        document.documentElement.dataset.theme = selectedTheme;

        try {
            localStorage.setItem(storageKey, selectedTheme);
        } catch {
            // Switching still works when browser storage is unavailable.
        }

        return selectedTheme;
    }

    let savedTheme = "dark";

    try {
        savedTheme = localStorage.getItem(storageKey) ?? "dark";
    } catch {
        // Use dark mode when browser storage is unavailable.
    }

    document.documentElement.dataset.theme =
        savedTheme === "light" ? "light" : "dark";

    return { getTheme, setTheme };
})();
