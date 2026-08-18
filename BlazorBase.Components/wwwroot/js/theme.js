const storageKey = 'blazorbase-theme';

export function apply(preference) {
    const root = document.documentElement;

    if (preference === 'System') {
        delete root.dataset.theme;
    } else {
        root.dataset.theme = preference.toLowerCase();
    }

    try {
        localStorage.setItem(storageKey, preference);
    } catch {
        // private browsing modes reject writes; the theme still applies for this session
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
