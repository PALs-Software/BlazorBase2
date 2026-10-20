const compactQuery = '(max-width: 1024px)';

let query = null;
let handler = null;

export function watchCompact(dotNetReference) {
    unwatchCompact();

    if (!window.matchMedia) {
        return false;
    }

    query = window.matchMedia(compactQuery);
    handler = event => dotNetReference.invokeMethodAsync('OnCompactChanged', event.matches);
    query.addEventListener('change', handler);

    return query.matches;
}

export function unwatchCompact() {
    if (query === null) {
        return;
    }

    query.removeEventListener('change', handler);
    query = null;
    handler = null;
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
