window.MusicRUsAuth = (() => {
    const storageKey = "musicrus.auth.session";
    const activityEvents = ["click", "keydown", "mousemove", "pointerdown", "scroll", "touchstart", "focus"];
    let watchersRegistered = false;
    let timeoutMs = 30 * 60 * 1000;

    function loadSession() {
        const stored = window.sessionStorage.getItem(storageKey);
        if (!stored) {
            return null;
        }

        try {
            const session = JSON.parse(stored);
            if (isExpired(session)) {
                clearSession();
                return null;
            }

            return session;
        }
        catch {
            clearSession();
            return null;
        }
    }

    function saveSession(session) {
        if (!session) {
            return;
        }

        session.lastActivityUtc = new Date().toISOString();
        window.sessionStorage.setItem(storageKey, JSON.stringify(session));
    }

    function clearSession() {
        window.sessionStorage.removeItem(storageKey);
    }

    function refreshActivity() {
        const session = loadSession();
        if (!session) {
            return;
        }

        saveSession(session);
    }

    function registerInactivityLogout(timeoutMinutes, redirectUrl) {
        timeoutMs = timeoutMinutes * 60 * 1000;

        if (watchersRegistered) {
            return;
        }

        watchersRegistered = true;

        const onActivity = () => refreshActivity();
        for (const eventName of activityEvents) {
            window.addEventListener(eventName, onActivity, { passive: true, capture: true });
        }

        const checkSession = () => {
            const session = loadSession();
            if (!session) {
                return;
            }

            if (isExpired(session)) {
                clearSession();
                window.location.assign(redirectUrl);
            }
        };

        checkSession();
        window.setInterval(checkSession, 60 * 1000);
    }

    return {
        loadSession,
        saveSession,
        clearSession,
        registerInactivityLogout
    };

    function isExpired(session) {
        if (!session || !session.lastActivityUtc) {
            return true;
        }

        const lastActivity = Date.parse(session.lastActivityUtc);
        return !Number.isFinite(lastActivity) || Date.now() - lastActivity >= timeoutMs;
    }
})();

window.addEventListener("pageshow", (event) => {
    if (event.persisted) {
        window.location.reload();
    }
});

window.initGoogleSignIn = (dotnetRef) => {
    const button = document.getElementById("google-btn");
    if (!button || !dotnetRef) {
        return;
    }

    const renderButton = () => {
        if (!window.google || !window.google.accounts || !window.google.accounts.id) {
            window.setTimeout(renderButton, 100);
            return;
        }

        button.innerHTML = "";
        window.google.accounts.id.initialize({
            client_id: "517391008713-pfep2mr1qepjcds929kfki412kt6gfg9.apps.googleusercontent.com",
            callback: (response) => {
                dotnetRef.invokeMethodAsync("OnGoogleToken", response.credential);
            }
        });
        window.google.accounts.id.renderButton(button, {
            theme: "outline",
            size: "large",
            width: Math.min(360, button.clientWidth || 360)
        });
    };

    renderButton();
};
