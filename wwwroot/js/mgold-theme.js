(function () {
    const themes = {
        "gold-premium": "Gold Premium",
        "diamond-silver": "Diamond Silver"
    };
    const order = Object.keys(themes);
    const storageKey = `mgold.theme.${window.MGoldTheme?.userKey || "anonymous"}`;
    const legacyStorageKey = "mgold.theme";
    const cookieName = "MGold.Theme";
    const root = document.documentElement;

    const normalize = (theme) => Object.prototype.hasOwnProperty.call(themes, theme) ? theme : "gold-premium";

    const readCookie = () => {
        const match = document.cookie.match(new RegExp(`(?:^|; )${cookieName}=([^;]*)`));
        return match ? decodeURIComponent(match[1]) : "";
    };

    const writeCookie = (theme) => {
        const maxAge = 60 * 60 * 24 * 365;
        const secure = window.location.protocol === "https:" ? "; Secure" : "";
        document.cookie = `${cookieName}=${encodeURIComponent(theme)}; Path=/; Max-Age=${maxAge}; SameSite=Lax${secure}`;
    };

    const getTheme = () => {
        try {
            const legacyTheme = window.MGoldTheme?.userKey === "anonymous" ? localStorage.getItem(legacyStorageKey) : "";
            return normalize(localStorage.getItem(storageKey) || legacyTheme || root.dataset.theme || window.MGoldTheme?.current || readCookie());
        } catch {
            return normalize(root.dataset.theme || window.MGoldTheme?.current || readCookie());
        }
    };

    const updateControls = (theme) => {
        document.querySelectorAll("[data-theme-label]").forEach((node) => {
            node.textContent = themes[theme];
        });

        document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
            button.setAttribute("aria-label", `Tema degistir. Aktif tema: ${themes[theme]}`);
            button.setAttribute("title", `Aktif tema: ${themes[theme]}`);
        });

        document.querySelectorAll("[data-theme-option]").forEach((button) => {
            button.setAttribute("aria-pressed", String(button.dataset.themeOption === theme));
        });
    };

    const persistRemote = (theme, button) => {
        const saveUrl = button?.dataset.themeSaveUrl || window.MGoldTheme?.saveUrl;
        if (!saveUrl) {
            return;
        }

        fetch(saveUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            body: JSON.stringify({ theme })
        }).catch(() => {
            // Local persistence still keeps the UI responsive if the session has expired.
        });
    };

    const setTheme = (theme, options = {}) => {
        const nextTheme = normalize(theme);
        root.dataset.theme = nextTheme;
        root.style.colorScheme = nextTheme === "diamond-silver" ? "light" : "dark";
        try {
            localStorage.setItem(storageKey, nextTheme);
            if (window.MGoldTheme?.userKey === "anonymous") {
                localStorage.setItem(legacyStorageKey, nextTheme);
            }
        } catch {
            // Storage can be unavailable in strict privacy modes.
        }
        writeCookie(nextTheme);
        updateControls(nextTheme);

        if (options.remote !== false) {
            persistRemote(nextTheme, options.source);
        }
    };

    const cycleTheme = (source) => {
        const current = getTheme();
        const currentIndex = order.indexOf(current);
        const nextTheme = order[(currentIndex + 1) % order.length];
        setTheme(nextTheme, { source });
    };

    setTheme(getTheme(), { remote: false });

    document.addEventListener("DOMContentLoaded", () => {
        updateControls(getTheme());
        document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
            button.addEventListener("click", () => cycleTheme(button));
        });

        document.querySelectorAll("[data-theme-option]").forEach((button) => {
            button.addEventListener("click", () => setTheme(button.dataset.themeOption, { source: button }));
        });
    });

    window.MGoldTheme = {
        ...(window.MGoldTheme || {}),
        themes,
        getTheme,
        setTheme
    };
})();
