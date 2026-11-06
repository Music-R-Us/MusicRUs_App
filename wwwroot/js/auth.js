// Keeps session storage and inactivity tracking private behind the Blazor auth helpers.
window.MusicRUsAuth = (() => {
    const storageKey = "musicrus.auth.session";
    const activityEvents = ["click", "keydown", "mousemove", "pointerdown", "scroll", "touchstart", "focus"];
    let watchersRegistered = false;
    let timeoutMs = 30 * 60 * 1000;

    // Restores this tab's session, discarding expired or invalid stored data.
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

    // Stores this tab's session with a fresh activity timestamp.
    function saveSession(session) {
        if (!session) {
            return;
        }

        session.lastActivityUtc = new Date().toISOString();
        window.sessionStorage.setItem(storageKey, JSON.stringify(session));
    }

    // Removes the saved session for this browser tab.
    function clearSession() {
        window.sessionStorage.removeItem(storageKey);
    }

    // Updates the activity timestamp if a valid session is still present.
    function refreshActivity() {
        const session = loadSession();
        if (!session) {
            return;
        }

        saveSession(session);
    }

    // Registers activity tracking once and periodically checks the stored session.
    function registerInactivityLogout(timeoutMinutes, redirectUrl) {
        timeoutMs = timeoutMinutes * 60 * 1000;

        if (watchersRegistered) {
            return;
        }

        watchersRegistered = true;

        // Treats each tracked browser event as session activity.
        const onActivity = () => refreshActivity();
        for (const eventName of activityEvents) {
            window.addEventListener(eventName, onActivity, { passive: true, capture: true });
        }

        // Loads the session and redirects if the returned session has expired.
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

    // Treats missing, invalid, or sufficiently old activity timestamps as expired.
    function isExpired(session) {
        if (!session || !session.lastActivityUtc) {
            return true;
        }

        const lastActivity = Date.parse(session.lastActivityUtc);
        return !Number.isFinite(lastActivity) || Date.now() - lastActivity >= timeoutMs;
    }
})();

// Reloads pages restored from the browser's back/forward cache to refresh session state.
window.addEventListener("pageshow", (event) => {
    if (event.persisted) {
        window.location.reload();
    }
});

// Waits for Google's script, renders its button, and passes the token back to Blazor.
window.initGoogleSignIn = (dotnetRef, buttonText = "Continue_with") => {
    const button = document.getElementById("google-btn");
    if (!button || !dotnetRef) {
        return;
    }

    // Retries until Google's SDK is ready, then creates the sign-in button.
    const renderButton = () => {
        if (!window.google || !window.google.accounts || !window.google.accounts.id) {
            window.setTimeout(renderButton, 100);
            return;
        }

        button.innerHTML = "";
        window.google.accounts.id.initialize({
            client_id: "1086295760706-3qic42cjff6dbej8836dmeiakgv16leh.apps.googleusercontent.com",
            // Passes Google's credential to the Blazor component for backend login.
            callback: (response) => {
                dotnetRef.invokeMethodAsync("OnGoogleToken", response.credential);
            }
        });
        window.google.accounts.id.renderButton(button, {
            theme: "outline",
            size: "large",
            text: buttonText,
            width: Math.min(360, button.clientWidth || 360)
        });
    };

    renderButton();
};
