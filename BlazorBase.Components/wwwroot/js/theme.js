const storageKey = 'blazorbase-theme';

export function apply(preference, persist) {
    const root = document.documentElement;

    if (preference === 'System') {
        delete root.dataset.theme;
    } else {
        root.dataset.theme = preference.toLowerCase();
    }

    // Only an explicit choice is remembered. Persisting on every load would store whatever the
    // profile seeded, and that stored value then outranks the profile forever - so changing the
    // preference on another device would never reach this browser again.
    if (persist) {
        try {
            localStorage.setItem(storageKey, preference);
        } catch {
            // private browsing modes reject writes; the theme still applies for this session
        }
    }

    const resolved = getComputedStyle(root).getPropertyValue('--color-bg-surface').trim();
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', resolved);
}

export function stored() {
    try {
        return localStorage.getItem(storageKey);
    } catch {
        return null;
    }
}

export function readToken(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}
