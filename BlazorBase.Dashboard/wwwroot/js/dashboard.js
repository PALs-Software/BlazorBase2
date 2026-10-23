const observers = new Map();
let nextObserverId = 1;

export function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export function isTabVisible() {
    return document.visibilityState !== 'hidden';
}

export function getUtcOffsetMinutes() {
    return -new Date().getTimezoneOffset();
}

const canvasContext = document.createElement('canvas').getContext('2d');

function normalize(color) {
    canvasContext.fillStyle = '#000000';
    canvasContext.fillStyle = color;
    return canvasContext.fillStyle;
}

function token(style, name) {
    const value = style.getPropertyValue(name).trim();
    return value ? normalize(value) : 'currentColor';
}

function readPalette() {
    const style = getComputedStyle(document.documentElement);
    const accent = token(style, '--color-accent');
    const surface = token(style, '--color-bg-elevated');

    return {
        text: token(style, '--color-text-primary'),
        textSecondary: token(style, '--color-text-secondary'),
        grid: token(style, '--color-border'),
        accent,
        accentMuted: normalize(`color-mix(in srgb, ${accent} 45%, ${surface})`),
        success: token(style, '--color-success'),
        warning: token(style, '--color-warning'),
        danger: token(style, '--color-danger'),
        neutral: token(style, '--color-text-muted')
    };
}

function applyChartDefaults(palette) {
    if (typeof Chart === 'undefined')
        return;

    Chart.defaults.color = palette.textSecondary;
    Chart.defaults.borderColor = palette.grid;
}

function refreshCharts() {
    if (typeof Chart === 'undefined' || !Chart.instances)
        return;

    Object.values(Chart.instances).forEach(chart => chart.update());
}

export function readChartPalette() {
    const palette = readPalette();
    applyChartDefaults(palette);
    return palette;
}

export function observeVisibility(reference) {
    const id = nextObserverId++;
    const handler = () => reference.invokeMethodAsync('Invoke', document.visibilityState !== 'hidden').catch(() => unobserve(id));

    document.addEventListener('visibilitychange', handler);
    observers.set(id, () => document.removeEventListener('visibilitychange', handler));
    return id;
}

export function observeTheme(reference) {
    const id = nextObserverId++;
    const query = window.matchMedia('(prefers-color-scheme: dark)');

    const handler = () => {
        applyChartDefaults(readPalette());
        refreshCharts();
        reference.invokeMethodAsync('Invoke', true).catch(() => unobserve(id));
    };

    const mutations = new MutationObserver(handler);
    mutations.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    query.addEventListener('change', handler);

    observers.set(id, () => {
        mutations.disconnect();
        query.removeEventListener('change', handler);
    });

    return id;
}

export function focusControl(partId, action) {
    const controls = document.querySelectorAll('[data-edit-action]');

    for (const control of controls) {
        if (control.dataset.editPart === partId && control.dataset.editAction === action) {
            control.focus();
            return true;
        }
    }

    return false;
}

export function focusById(id) {
    const element = document.getElementById(id);

    if (!element)
        return false;

    element.focus();
    return true;
}

export function unobserve(id) {
    const dispose = observers.get(id);

    if (!dispose)
        return;

    observers.delete(id);
    dispose();
}
