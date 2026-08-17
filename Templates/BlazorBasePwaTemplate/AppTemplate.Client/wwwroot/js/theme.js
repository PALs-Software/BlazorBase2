const STORAGE_KEY = 'apptemplate-theme';

export function prefersDark() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function apply(preference) {
    const resolved = preference === 'system'
        ? (prefersDark() ? 'dark' : 'light')
        : preference;

    document.documentElement.dataset.theme = resolved;
    localStorage.setItem(STORAGE_KEY, preference);

    const themeColor = getComputedStyle(document.documentElement)
        .getPropertyValue('--color-surface')
        .trim();

    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColor);
}

export function stored() {
    return localStorage.getItem(STORAGE_KEY);
}

export function readToken(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}
