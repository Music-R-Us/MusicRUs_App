window.MusicRUsProfileColor = (() => {
    const colors = new Map();
    const key = userId => `musicrus-profile-color:${userId}`;
    return {
        load(userId) {
            if (colors.has(userId)) return colors.get(userId);
            try {
                return localStorage.getItem(key(userId));
            } catch {
                return null;
            }
        },
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
