const compactQuery = '(max-width: 1024px)';
const phoneMedia = '(max-width: 768px)';
const collapsedKey = 'blazorbase-nav-collapsed';
const groupKeyPrefix = 'blazorbase-nav-group:';
const keyboardAttribute = 'data-base-keyboard-open';
const keyboardThreshold = 120;
const drawerId = 'base-navigation';
const tabbableSelector = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), fluent-anchor[href], fluent-button:not([disabled]), fluent-select:not([disabled]), [tabindex]:not([tabindex="-1"])';

let query = null;
let handler = null;
let reference = null;
let phoneQuery = null;
let phoneHandler = null;
let phoneReference = null;
let keyboardBaseline = 0;
let keyboardWidth = 0;

export function watchCompact(dotNetReference) {
    unwatchCompact();

    if (!window.matchMedia) {
        return false;
    }

    reference = dotNetReference;
    query = window.matchMedia(compactQuery);
    handler = event => dotNetReference.invokeMethodAsync('OnCompactChanged', event.matches);
    query.addEventListener('change', handler);
    document.addEventListener('keydown', onDrawerKeyDown, true);

    return query.matches;
}

export function unwatchCompact() {
    document.removeEventListener('keydown', onDrawerKeyDown, true);

    if (query === null) {
        return;
    }

    query.removeEventListener('change', handler);
    query = null;
    handler = null;
    reference = null;
}

export function readCollapsed() {
    try {
        return localStorage.getItem(collapsedKey) === 'true';
    } catch {
        return false;
    }
}

export function writeCollapsed(collapsed) {
    try {
        if (collapsed) {
            localStorage.setItem(collapsedKey, 'true');
        } else {
            localStorage.removeItem(collapsedKey);
        }
    } catch {
        // storage can be unavailable (private browsing); the choice then only lasts for this session
    }
}

/**
 * Reads which navigation groups the user left expanded or collapsed. Only keys with a remembered choice are
 * part of the result, so a group never touched stays at its default.
 */
export function readGroupStates(keys) {
    const states = {};

    try {
        for (const key of keys ?? []) {
            const value = localStorage.getItem(groupKeyPrefix + key);

            if (value === 'true' || value === 'false') {
                states[key] = value === 'true';
            }
        }
    } catch {
        // storage can be unavailable (private browsing); the groups then start at their defaults
    }

    return states;
}

export function writeGroupState(key, expanded) {
    try {
        localStorage.setItem(groupKeyPrefix + key, expanded ? 'true' : 'false');
    } catch {
        // storage can be unavailable (private browsing); the choice then only lasts for this session
    }
}

/**
 * Tells the layout when the viewport is a phone, which is when the bottom navigation bar is shown, and
 * marks the layout while the on-screen keyboard is open so the bar can get out of its way. The keyboard is
 * recognised as an editable element holding the focus while the visible height has dropped well below the
 * tallest height seen at the current width.
 */
export function watchPhone(dotNetReference) {
    unwatchPhone();

    phoneReference = dotNetReference;

    if (!window.matchMedia) {
        return false;
    }

    phoneQuery = window.matchMedia(phoneMedia);
    phoneHandler = event => dotNetReference.invokeMethodAsync('OnPhoneChanged', event.matches);
    phoneQuery.addEventListener('change', phoneHandler);
    keyboardBaseline = viewportHeight();
    keyboardWidth = window.innerWidth;
    document.addEventListener('focusin', updateKeyboardState);
    document.addEventListener('focusout', deferKeyboardState);
    window.addEventListener('resize', updateKeyboardState);
    window.visualViewport?.addEventListener('resize', updateKeyboardState);

    return phoneQuery.matches;
}

export function unwatchPhone() {
    document.removeEventListener('focusin', updateKeyboardState);
    document.removeEventListener('focusout', deferKeyboardState);
    window.removeEventListener('resize', updateKeyboardState);
    window.visualViewport?.removeEventListener('resize', updateKeyboardState);
    layoutRoot()?.removeAttribute(keyboardAttribute);

    if (phoneQuery !== null) {
        phoneQuery.removeEventListener('change', phoneHandler);
    }

    phoneQuery = null;
    phoneHandler = null;
    phoneReference = null;
}

function layoutRoot() {
    return document.querySelector('.layout-root');
}

function viewportHeight() {
    return window.visualViewport?.height ?? window.innerHeight;
}

function deepActiveElement() {
    let active = document.activeElement;

    while (active?.shadowRoot?.activeElement) {
        active = active.shadowRoot.activeElement;
    }

    return active;
}

function isEditable(element) {
    if (!element) {
        return false;
    }

    if (element.isContentEditable) {
        return true;
    }

    const tag = element.tagName;

    if (tag === 'TEXTAREA' || tag === 'SELECT') {
        return true;
    }

    return tag === 'INPUT' && !['button', 'checkbox', 'radio', 'submit', 'reset', 'range', 'color', 'file', 'image'].includes(element.type);
}

function deferKeyboardState() {
    setTimeout(updateKeyboardState, 0);
}

function updateKeyboardState() {
    const root = layoutRoot();

    if (!root) {
        return;
    }

    if (window.innerWidth !== keyboardWidth) {
        keyboardWidth = window.innerWidth;
        keyboardBaseline = 0;
    }

    const height = viewportHeight();
    keyboardBaseline = Math.max(keyboardBaseline, height);

    const open = isEditable(deepActiveElement()) && keyboardBaseline - height > keyboardThreshold;

    if (open) {
        root.setAttribute(keyboardAttribute, '');
    } else {
        root.removeAttribute(keyboardAttribute);
    }
}

export function focusFirst(container) {
    if (!container) {
        return;
    }

    const target = container.querySelector('a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])');
    (target ?? container).focus();
}

/**
 * Moves the focus into a modal panel and keeps Tab and Shift+Tab cycling inside it. The listener lives on the
 * panel element, so it goes away with the panel.
 */
export function trapFocus(container) {
    if (!container) {
        return;
    }

    container.addEventListener('keydown', event => {
        if (event.key !== 'Tab' || event.defaultPrevented) {
            return;
        }

        const inside = [...container.querySelectorAll(tabbableSelector)].filter(isVisible);

        if (inside.length === 0) {
            event.preventDefault();
            container.focus();
            return;
        }

        const first = inside[0];
        const last = inside[inside.length - 1];
        const active = document.activeElement;

        if (event.shiftKey && (active === first || active === container || !container.contains(active))) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && (active === last || !container.contains(active))) {
            event.preventDefault();
            first.focus();
        }
    });

    const target = container.querySelector('[data-autofocus]');
    (target ?? container.querySelector(tabbableSelector) ?? container).focus();
}

export function restoreFocus(main, drawer) {
    const active = document.activeElement;

    if (active === document.body || (drawer && drawer.contains(active))) {
        main?.focus();
    }
}

function isVisible(element) {
    return element.getClientRects().length > 0 && getComputedStyle(element).visibility !== 'hidden';
}

/**
 * While the off-canvas drawer is open it behaves like a modal dialog for the keyboard: Tab and Shift+Tab
 * cycle through the drawer and its toggle button instead of leaving for the page behind it, and Escape
 * closes it from wherever the focus is. Escape pressed inside the drawer is left to the component.
 */
function onDrawerKeyDown(event) {
    const drawer = document.getElementById(drawerId);

    if (!drawer || !drawer.classList.contains('open') || !query?.matches) {
        return;
    }

    const active = document.activeElement;
    const toggle = document.querySelector(`[aria-controls="${drawerId}"]`);

    if (event.key === 'Escape') {
        const popupOpen = active !== toggle && active?.getAttribute('aria-expanded') === 'true';

        if (!drawer.contains(active) && !popupOpen) {
            reference?.invokeMethodAsync('OnEscapeOutsideDrawer');
        }

        return;
    }

    if (event.key !== 'Tab' || event.defaultPrevented) {
        return;
    }

    const inside = [...drawer.querySelectorAll(tabbableSelector)].filter(isVisible);
    const cycle = toggle ? [toggle, ...inside] : inside;

    if (cycle.length === 0) {
        return;
    }

    const index = cycle.indexOf(active);
    const last = cycle.length - 1;

    if (index === -1) {
        event.preventDefault();
        cycle[event.shiftKey ? last : Math.min(1, last)].focus();
        return;
    }

    if (event.shiftKey && index === 0) {
        event.preventDefault();
        cycle[last].focus();
    } else if (!event.shiftKey && index === last) {
        event.preventDefault();
        cycle[0].focus();
    }
}
