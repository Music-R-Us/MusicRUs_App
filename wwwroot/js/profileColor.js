// Exposes profile-color storage while keeping the per-tab cache private.
window.MusicRUsProfileColor = (() => {
    const colors = new Map();
    // Gives each user's saved color a separate browser storage key.
    const key = userId => `musicrus-profile-color:${userId}`;
    return {
        // Prefers the tab's saved choice, then checks persistent browser storage.
        load(userId) {
            if (colors.has(userId)) return colors.get(userId);
            try {
                return localStorage.getItem(key(userId));
            } catch {
                return null;
            }
        },
        // Saves the choice in memory and persists it when browser storage is available.
        save(userId, color) {
            colors.set(userId, color);
            try {
                localStorage.setItem(key(userId), color);
            } catch {
                // Keep the choice for this tab when browser storage is unavailable.
            }
        }
    };
})();
