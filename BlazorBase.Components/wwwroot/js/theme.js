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

let systemQuery = null;
let systemHandler = null;

// While the preference is "System" nothing is stamped on the root element, so a change of the
// operating system's own setting repaints the page through prefers-color-scheme alone - without .NET
// ever noticing. Anything derived from the tokens on the managed side (the accent handed to FluentUI,
// the theme-color meta) would keep the values of the theme that just went away.
export function watchSystem(dotNetReference) {
    unwatchSystem();

    if (!window.matchMedia) {
        return;
    }

    systemQuery = window.matchMedia('(prefers-color-scheme: dark)');
    systemHandler = () => dotNetReference.invokeMethodAsync('OnSystemThemeChangedAsync');
    systemQuery.addEventListener('change', systemHandler);
}

export function unwatchSystem() {
    if (systemQuery === null) {
        return;
    }

    systemQuery.removeEventListener('change', systemHandler);
    systemQuery = null;
    systemHandler = null;
}
