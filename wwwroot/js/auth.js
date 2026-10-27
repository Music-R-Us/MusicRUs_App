(() => {
    const sessionKey = "MusicRUs.auth.session";
    let inactivityTimer;
    let activityHandler;

    window.MusicRUsAuth = {
        loadSession() {
            const stored = localStorage.getItem(sessionKey);
            if (!stored) return null;
            try {
                const session = JSON.parse(stored);
                if (session && typeof session.username === "string" && session.username.trim()) {
                    return session;
                }
            } catch {
                // Discard corrupted session data so the login page can still open.
            }
            localStorage.removeItem(sessionKey);
            return null;
        },
        saveSession(session) {
            localStorage.setItem(sessionKey, JSON.stringify(session));
        },
        clearSession() {
            localStorage.removeItem(sessionKey);
        },
        registerInactivityLogout(minutes, redirectUrl) {
            const events = ["pointerdown", "keydown", "scroll", "touchstart"];
            clearTimeout(inactivityTimer);
            if (activityHandler) {
                events.forEach(event => window.removeEventListener(event, activityHandler));
            }
            activityHandler = () => {
                clearTimeout(inactivityTimer);
                inactivityTimer = setTimeout(() => {
                    if (localStorage.getItem(sessionKey)) {
                        window.MusicRUsAuth.clearSession();
                        window.location.assign(redirectUrl);
                    }
                }, minutes * 60 * 1000);
            };
            events.forEach(event => window.addEventListener(event, activityHandler, { passive: true }));
            activityHandler();
        }
    };

    window.initGoogleSignIn = () => {
        const container = document.getElementById("google-btn");
        if (container) {
            container.textContent = "Google sign-in is not configured yet.";
        }
    };
})();
