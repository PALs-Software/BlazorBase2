const parts = new WeakMap();
let rememberedFocus = null;

function rowOf(element) {
    return element && element.closest ? element.closest('[data-listpart-row]') : null;
}

// The keys of a list part are only taken where the keyboard stands on one of the part's own controls (the item label,
// the select circle, the row menu button). The inline editors of an item keep every key to themselves, so an arrow key
// in a text field still moves the caret.
export function observe(container, dotNetRef) {
    if (!container || parts.has(container))
        return;

    const state = { container, dotNetRef, suppressContextMenuUntil: 0 };

    const setShift = held => {
        if (held)
            container.dataset.shift = 'true';
        else
            delete container.dataset.shift;
    };

    state.onKeyDown = event => {
        if (event.altKey || event.defaultPrevented)
            return;

        const target = event.target && event.target.closest ? event.target.closest('[data-listpart-key]') : null;

        if (!target || !container.contains(target))
            return;

        const row = rowOf(target);

        if (!row)
            return;

        const rowIndex = Number(row.dataset.listpartRow);
        const kind = target.dataset.listpartKey;

        if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
            event.preventDefault();
            state.suppressContextMenuUntil = performance.now() + 1000;
            const anchor = (row.querySelector('[data-listpart-key="menu"]') || target).getBoundingClientRect();
            dotNetRef.invokeMethodAsync('OnContextMenuKeyAsync', rowIndex, anchor.left, anchor.top, anchor.right, anchor.bottom).catch(() => { });
            return;
        }

        const ctrl = event.ctrlKey || event.metaKey;
        const key = event.key;
        const selectAll = ctrl && (key === 'a' || key === 'A');
        const activation = key === ' ' || key === 'Enter';

        if (kind === 'menu' && activation)
            return;

        if (!(selectAll || key === 'ArrowUp' || key === 'ArrowDown' || (!ctrl && activation)))
            return;

        if (selectAll || key === ' ' || key === 'ArrowUp' || key === 'ArrowDown')
            event.preventDefault();

        dotNetRef.invokeMethodAsync('OnKeyAsync', key, event.shiftKey, ctrl, rowIndex).catch(() => { });
    };

    // A card closes on Escape through a listener that runs before any element sees the key. While the part's menu is open,
    // or while the keyboard stands on one of the part's controls and something is selected, Escape belongs to the part
    // (it closes the menu, or clears the selection), so it is taken at the window, before the card can react.
    state.onEscape = event => {
        if (event.key !== 'Escape' || event.defaultPrevented)
            return;

        const menuOpen = container.querySelector('.context-menu') !== null;
        const onControl = event.target && event.target.closest && container.contains(event.target) && event.target.closest('[data-listpart-key]') !== null;

        if (!menuOpen && !(onControl && container.hasAttribute('data-has-selection')))
            return;

        event.preventDefault();
        event.stopPropagation();
        dotNetRef.invokeMethodAsync('OnEscapeAsync').catch(() => { });
    };

    state.onShiftKey = event => {
        if (event.key === 'Shift')
            setShift(event.type === 'keydown');
    };

    state.onWindowBlur = () => setShift(false);

    state.onMouseDown = event => {
        if (event.shiftKey) {
            setShift(true);
            window.getSelection()?.removeAllRanges();
        }
    };

    state.onDoubleClick = () => window.getSelection()?.removeAllRanges();

    state.onContextMenuCapture = event => {
        if (!state.suppressContextMenuUntil || performance.now() > state.suppressContextMenuUntil)
            return;

        state.suppressContextMenuUntil = 0;
        event.preventDefault();
        event.stopPropagation();
    };

    container.addEventListener('keydown', state.onKeyDown);
    container.addEventListener('mousedown', state.onMouseDown, true);
    container.addEventListener('dblclick', state.onDoubleClick);
    container.addEventListener('contextmenu', state.onContextMenuCapture, true);
    document.addEventListener('keydown', state.onShiftKey);
    document.addEventListener('keyup', state.onShiftKey);
    window.addEventListener('blur', state.onWindowBlur);
    window.addEventListener('keydown', state.onEscape, true);
    parts.set(container, state);
}

export function unobserve(container) {
    const state = parts.get(container);

    if (!state)
        return;

    container.removeEventListener('keydown', state.onKeyDown);
    container.removeEventListener('mousedown', state.onMouseDown, true);
    container.removeEventListener('dblclick', state.onDoubleClick);
    container.removeEventListener('contextmenu', state.onContextMenuCapture, true);
    document.removeEventListener('keydown', state.onShiftKey);
    document.removeEventListener('keyup', state.onShiftKey);
    window.removeEventListener('blur', state.onWindowBlur);
    window.removeEventListener('keydown', state.onEscape, true);
    parts.delete(container);
}

// Puts the keyboard on the row that the selection just moved to, so the next arrow key continues from there.
export function focusRow(container, index) {
    const row = container ? container.querySelector(`[data-listpart-row="${index}"]`) : null;
    const target = row ? (row.querySelector('[data-listpart-focus]') || row.querySelector('[data-listpart-key]')) : null;

    if (target)
        target.focus();
}

export function measure(element) {
    if (!element)
        return null;

    const box = element.getBoundingClientRect();
    return [box.width, box.height, document.documentElement.clientWidth, document.documentElement.clientHeight];
}

// The box of the row menu button a click landed on, so the menu opens beside the button; a keyboard-driven click has
// no usable pointer position, so the focused element is the fallback.
export function menuButtonRect(clientX, clientY) {
    const hit = document.elementFromPoint(clientX, clientY);
    const button = (hit && hit.closest('[data-listpart-key="menu"]'))
        || (document.activeElement && document.activeElement.closest('[data-listpart-key="menu"]'));

    if (!button)
        return null;

    const box = button.getBoundingClientRect();
    return [box.left, box.top, box.right, box.bottom];
}

export function rememberFocus() {
    const active = document.activeElement;
    const button = active && active.closest ? active.closest('[data-listpart-key="menu"]') : null;
    const row = rowOf(button);
    const control = row ? (row.querySelector('[data-listpart-focus]') || row.querySelector('[data-listpart-key]:not([data-listpart-key="menu"])')) : null;

    rememberedFocus = control || active;
}

export function restoreFocus() {
    const element = rememberedFocus;
    rememberedFocus = null;

    if (element && document.contains(element))
        element.focus({ preventScroll: true });
}
