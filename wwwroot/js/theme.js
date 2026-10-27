window.MusicRUsTheme = (() => {
    const storageKey = "musicrus-theme";

    function getTheme() {
        return document.documentElement.dataset.theme === "light"
            ? "light"
            : "dark";
    }

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