const itemSelector = '[role="menuitem"], [role="menuitemradio"]';

let active = null;

/**
 * Starts watching the page for the ways an open user menu is dismissed: a pointer press or the focus moving outside
 * the root element, and Escape. Arrow keys, Home and End move the focus between the entries; Space activates a link
 * entry like Enter does.
 */
export function attach(root, reference) {
    detach();

    const dismiss = restoreFocus => reference.invokeMethodAsync('OnDismissRequested', restoreFocus);
    const onOutside = event => {
        if (!root.contains(event.target)) {
            dismiss(false);
        }
    };
    const onKeyDown = event => {
        if (event.key === 'Escape') {
            event.preventDefault();
            dismiss(true);
            return;
        }

        if (!root.contains(document.activeElement)) {
            return;
        }

        if (event.key === ' ' && document.activeElement.matches('a[role="menuitem"]')) {
            event.preventDefault();
            document.activeElement.click();
            return;
        }

        const items = Array.from(root.querySelectorAll(itemSelector));
        const index = items.indexOf(document.activeElement);
        const target = nextItem(event.key, items, index);
        if (target === null) {
            return;
        }

        event.preventDefault();
        target.focus();
    };

    document.addEventListener('pointerdown', onOutside, true);
    document.addEventListener('focusin', onOutside, true);
    document.addEventListener('keydown', onKeyDown, true);
    active = { onOutside, onKeyDown };
}

/** Stops watching; safe to call when nothing is attached. */
export function detach() {
    if (active === null) {
        return;
    }

    document.removeEventListener('pointerdown', active.onOutside, true);
    document.removeEventListener('focusin', active.onOutside, true);
    document.removeEventListener('keydown', active.onKeyDown, true);
    active = null;
}

/** Moves the focus to the first or the last entry of the menu. */
export function focusItem(popup, position) {
    const items = popup ? Array.from(popup.querySelectorAll(itemSelector)) : [];
    const target = position === 'last' ? items[items.length - 1] : items[0];
    target?.focus({ preventScroll: true });
}

function nextItem(key, items, index) {
    if (items.length === 0) {
        return null;
    }

    switch (key) {
        case 'ArrowDown':
            return items[index < 0 ? 0 : (index + 1) % items.length];
        case 'ArrowUp':
            return items[index < 0 ? items.length - 1 : (index - 1 + items.length) % items.length];
        case 'Home':
            return index < 0 ? null : items[0];
        case 'End':
            return index < 0 ? null : items[items.length - 1];
        default:
            return null;
    }
}
