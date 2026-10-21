const compactQuery = '(max-width: 1024px)';
const collapsedKey = 'blazorbase-nav-collapsed';
const drawerId = 'base-navigation';
const tabbableSelector = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), fluent-anchor[href], fluent-button:not([disabled]), fluent-select:not([disabled]), [tabindex]:not([tabindex="-1"])';

let query = null;
let handler = null;
let reference = null;

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

export function focusFirst(container) {
    if (!container) {
        return;
    }

    const target = container.querySelector('a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])');
    (target ?? container).focus();
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
