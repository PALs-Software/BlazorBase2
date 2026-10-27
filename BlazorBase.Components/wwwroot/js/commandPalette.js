let shortcut = null;
let session = null;

/**
 * Listens on the whole page for the keys that open the command palette: Ctrl or Cmd plus K toggles it, and a plain
 * slash opens it while no editable element has the focus. The .NET side decides what to do with it.
 */
export function registerShortcut(reference, slashEnabled) {
    unregisterShortcut();

    const onKeyDown = event => {
        if (event.isComposing || event.repeat) {
            return;
        }

        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && (event.key === 'k' || event.key === 'K')) {
            event.preventDefault();
            reference.invokeMethodAsync('OnShortcut', true);
            return;
        }

        if (slashEnabled && event.key === '/' && !event.ctrlKey && !event.metaKey && !event.altKey && !isEditable(event)) {
            event.preventDefault();
            reference.invokeMethodAsync('OnShortcut', false);
        }
    };

    document.addEventListener('keydown', onKeyDown, true);
    shortcut = { onKeyDown };
}

/** Stops listening for the shortcut keys; safe to call when nothing is registered. */
export function unregisterShortcut() {
    if (shortcut === null) {
        return;
    }

    document.removeEventListener('keydown', shortcut.onKeyDown, true);
    shortcut = null;
}

/**
 * Called when the palette is shown: remembers the element that had the focus, moves the focus to the input, keeps
 * the arrow, Enter and (for an empty input) Home and End keys from acting on the text field, keeps Tab inside the
 * palette, closes it on Escape wherever the focus is (through the .NET reference) and keeps a mouse press on a
 * non-interactive spot of the panel from taking the focus away from the input.
 */
export function activate(panel, input, reference) {
    if (session === null) {
        const previous = document.activeElement;
        const onKeyDown = event => {
            if (event.key === 'Tab') {
                trapTab(panel, event);
                return;
            }

            if (event.target !== input) {
                return;
            }

            const movesList = event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter'
                || ((event.key === 'Home' || event.key === 'End') && (input.value === '' || event.ctrlKey));
            if (movesList) {
                event.preventDefault();
            }
        };

        const onDocumentKeyDown = event => {
            if (event.key !== 'Escape' || event.isComposing || !reference) {
                return;
            }

            reference.invokeMethodAsync('OnEscape').catch(() => { });
        };

        const onMouseDown = event => {
            if (keepsFocusOnPress(event)) {
                return;
            }

            event.preventDefault();
            if (document.activeElement !== input) {
                input.focus({ preventScroll: true });
            }
        };

        panel.addEventListener('keydown', onKeyDown);
        panel.addEventListener('mousedown', onMouseDown);
        document.addEventListener('keydown', onDocumentKeyDown, true);
        session = { previous, panel, onKeyDown, onMouseDown, onDocumentKeyDown };
    }

    input.focus({ preventScroll: true });
    const end = input.value.length;
    input.setSelectionRange(end, end);
}

/** Called when the palette is hidden: removes the key handling and returns the focus to where it was. */
export function deactivate() {
    if (session === null) {
        return;
    }

    const { previous, panel, onKeyDown, onMouseDown, onDocumentKeyDown } = session;
    session = null;
    panel.removeEventListener('keydown', onKeyDown);
    panel.removeEventListener('mousedown', onMouseDown);
    document.removeEventListener('keydown', onDocumentKeyDown, true);

    if (previous && previous.isConnected && typeof previous.focus === 'function') {
        previous.focus({ preventScroll: true });
    }
}

/** Scrolls the highlighted row into view; the first row also reveals the group title above it. */
export function scrollToActive(list) {
    const active = list?.querySelector('[aria-selected="true"]');
    if (!active) {
        return;
    }

    if (active === list.querySelector('[role="option"]')) {
        list.scrollTop = 0;
        return;
    }

    active.scrollIntoView({ block: 'nearest' });
}

/** Reads the identifiers of the recently used entries; an empty list when storage is unavailable. */
export function readRecent(key) {
    try {
        const parsed = JSON.parse(window.localStorage.getItem(key) ?? '[]');
        return Array.isArray(parsed) ? parsed.filter(item => typeof item === 'string') : [];
    } catch {
        return [];
    }
}

/** Stores the identifiers of the recently used entries; silently ignored when storage is unavailable. */
export function writeRecent(key, ids) {
    try {
        window.localStorage.setItem(key, JSON.stringify(ids));
    } catch {
    }
}

/** Opens an address in a new tab without giving the new page access to this one. */
export function openExternal(url) {
    window.open(url, '_blank', 'noopener,noreferrer');
}

function keepsFocusOnPress(event) {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
        return true;
    }

    if (target.closest('input, textarea, select, button, a[href]')) {
        return true;
    }

    const onVerticalScrollbar = target.scrollHeight > target.clientHeight && event.offsetX >= target.clientWidth;
    const onHorizontalScrollbar = target.scrollWidth > target.clientWidth && event.offsetY >= target.clientHeight;
    return onVerticalScrollbar || onHorizontalScrollbar;
}

function isEditable(event) {
    const target = (typeof event.composedPath === 'function' ? event.composedPath()[0] : null) ?? event.target;
    if (!(target instanceof HTMLElement)) {
        return false;
    }

    return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
}

function trapTab(panel, event) {
    const focusable = Array.from(panel.querySelectorAll('input, button:not([disabled]), [tabindex="0"]'))
        .filter(element => element.offsetParent !== null);
    if (focusable.length === 0) {
        event.preventDefault();
        return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const current = document.activeElement;

    if (event.shiftKey && (current === first || !panel.contains(current))) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && (current === last || !panel.contains(current))) {
        event.preventDefault();
        first.focus();
    }
}
