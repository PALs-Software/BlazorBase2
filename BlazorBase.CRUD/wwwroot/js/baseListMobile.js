const layouts = new WeakMap();

function widthOf(root) {
    return root.getBoundingClientRect().width;
}

function isNarrow(root, breakpoint) {
    const width = widthOf(root);
    return breakpoint > 0 && width > 0 && width < breakpoint;
}

function release(grid) {
    const state = layouts.get(grid);
    if (!state)
        return;

    state.observer.disconnect();
    layouts.delete(grid);
}

// Watches the width of the list (the container, not the window, so a list inside a narrow panel of a wide
// screen also switches) and tells the list when it crosses the breakpoint. The current answer is returned so the
// first page is already loaded into the right layout.
export function observeLayout(grid, dotNetRef, breakpoint) {
    const root = grid && grid.closest ? grid.closest('.base-list') : null;
    if (!root)
        return false;

    release(grid);

    const state = { root, dotNetRef, breakpoint, narrow: isNarrow(root, breakpoint) };

    state.observer = new ResizeObserver(() => {
        if (!root.isConnected) {
            release(grid);
            return;
        }

        const narrow = isNarrow(root, state.breakpoint);
        if (narrow === state.narrow)
            return;

        state.narrow = narrow;
        grid.dataset.mode = 'fits';
        state.dotNetRef.invokeMethodAsync('OnMobileLayoutChangedAsync', narrow).catch(() => { });
    });

    state.observer.observe(root);
    layouts.set(grid, state);

    return state.narrow;
}

export function disposeLayout(grid) {
    release(grid);
    releaseTiles(grid);
}

const tilePresses = new WeakMap();
const LONG_PRESS_MILLISECONDS = 500;
const LONG_PRESS_TOLERANCE_PIXELS = 10;
const TOUCH_CONTEXT_MENU_WINDOW_MILLISECONDS = 2000;
const IGNORED_PRESS_TARGETS = '.base-list-card-more, .base-list-card-actions, .base-list-card-check';

function releaseTiles(grid) {
    const state = tilePresses.get(grid);
    if (!state)
        return;

    clearTimeout(state.timer);
    grid.removeEventListener('pointerdown', state.onPointerDown);
    grid.removeEventListener('pointermove', state.onPointerMove);
    grid.removeEventListener('pointerup', state.onPointerEnd);
    grid.removeEventListener('pointercancel', state.onPointerEnd);
    grid.removeEventListener('contextmenu', state.onContextMenu, true);
    grid.removeEventListener('click', state.onClick, true);
    window.removeEventListener('scroll', state.onScroll, true);
    tilePresses.delete(grid);
}

// A long press on a tile (touch or pen, never the mouse) tells the list which tile it was, by the position the tile
// carries in data-tile-index. It is cancelled by moving the finger, by scrolling and by lifting it, so a swipe never
// selects. The context menu the browser raises for a long press is swallowed (the list's own row menu is the three-dot
// button), and so is the click that follows the release, so the tile does not also open.
export function observeTiles(grid, dotNetRef) {
    if (!grid || tilePresses.has(grid))
        return;

    const state = { timer: 0, tile: null, startX: 0, startY: 0, fired: false, swallowClick: false, lastTouchAt: -Infinity };

    const cancel = () => {
        clearTimeout(state.timer);
        state.timer = 0;
        state.tile = null;
    };

    const fire = tile => {
        if (!tile || state.fired)
            return;

        const index = parseInt(tile.dataset.tileIndex, 10);
        if (Number.isNaN(index))
            return;

        state.fired = true;
        state.swallowClick = true;
        cancel();

        try {
            if (navigator.vibrate)
                navigator.vibrate(12);
        } catch (error) {
        }

        dotNetRef.invokeMethodAsync('OnTileLongPressAsync', index).catch(() => { });
    };

    state.onPointerDown = event => {
        state.swallowClick = false;
        state.fired = false;
        cancel();

        if (event.pointerType === 'mouse' || !event.isPrimary)
            return;

        state.lastTouchAt = performance.now();

        const target = event.target instanceof Element ? event.target : null;
        const tile = target ? target.closest('.base-list-card[data-tile-index]') : null;

        if (!tile || target.closest(IGNORED_PRESS_TARGETS))
            return;

        state.tile = tile;
        state.startX = event.clientX;
        state.startY = event.clientY;
        state.timer = setTimeout(() => fire(state.tile), LONG_PRESS_MILLISECONDS);
    };

    state.onPointerMove = event => {
        if (!state.timer)
            return;

        if (Math.hypot(event.clientX - state.startX, event.clientY - state.startY) > LONG_PRESS_TOLERANCE_PIXELS)
            cancel();
    };

    state.onPointerEnd = () => cancel();

    state.onScroll = () => cancel();

    state.onContextMenu = event => {
        const touched = performance.now() - state.lastTouchAt < TOUCH_CONTEXT_MENU_WINDOW_MILLISECONDS;
        const fromTouch = touched || event.pointerType === 'touch' || event.pointerType === 'pen';

        if (!fromTouch)
            return;

        event.preventDefault();
        event.stopPropagation();

        const target = event.target instanceof Element ? event.target : null;
        const tile = state.tile || (target ? target.closest('.base-list-card[data-tile-index]') : null);

        if (tile && !(target && target.closest(IGNORED_PRESS_TARGETS)))
            fire(tile);
    };

    state.onClick = event => {
        if (!state.swallowClick)
            return;

        state.swallowClick = false;
        event.preventDefault();
        event.stopPropagation();
    };

    grid.addEventListener('pointerdown', state.onPointerDown);
    grid.addEventListener('pointermove', state.onPointerMove);
    grid.addEventListener('pointerup', state.onPointerEnd);
    grid.addEventListener('pointercancel', state.onPointerEnd);
    grid.addEventListener('contextmenu', state.onContextMenu, true);
    grid.addEventListener('click', state.onClick, true);
    window.addEventListener('scroll', state.onScroll, true);
    tilePresses.set(grid, state);
}
