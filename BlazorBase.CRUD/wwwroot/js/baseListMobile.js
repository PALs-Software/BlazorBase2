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
}
