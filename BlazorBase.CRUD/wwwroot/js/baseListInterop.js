export function isPointOnDataRow(gridElement, clientX, clientY) {
    const element = document.elementFromPoint(clientX, clientY);
    if (!element)
        return false;

    // FluentDataGrid renders a table: rows are <tr class="fluent-data-grid-row">, and only header
    // rows carry a row-type attribute. Matching the class rather than an element name keeps this in
    // step with BaseList.razor.css, which already targets tr.fluent-data-grid-row:not([row-type]).
    const row = element.closest('.fluent-data-grid-row');
    if (row === null || !gridElement.contains(row))
        return false;

    return !row.hasAttribute('row-type');
}

// Focus return for the overlays. The element the keyboard came from is a data-grid cell or a Fluent
// UI button - neither hands Blazor an ElementReference to focus back onto, so the browser remembers
// it instead. Keyed per surface because a card can host a list part while its own list is behind it.
// A card opened from a row can outlive the row element: the router may rebuild the whole list for the
// new URL. The cell is therefore also described by position (list, row, column) so focus can be put back
// on the equivalent cell of the rebuilt list.
const rememberedFocus = new Map();

function describeCell(element) {
    const cell = element && element.closest ? element.closest('td[role="gridcell"]') : null;
    const row = cell ? cell.closest('tr') : null;
    const list = cell ? cell.closest('.base-list') : null;

    if (!row || !list)
        return null;

    return {
        listIndex: Array.from(document.querySelectorAll('.base-list')).indexOf(list),
        rowIndex: row.getAttribute('aria-rowindex'),
        columnIndex: cell.getAttribute('col-index')
    };
}

function findCell(description) {
    if (!description)
        return null;

    const list = document.querySelectorAll('.base-list')[description.listIndex];
    if (!list)
        return null;

    const row = list.querySelector(`tr[aria-rowindex="${description.rowIndex}"]`);
    return row ? row.querySelector(`td[col-index="${description.columnIndex}"]`) : null;
}

export function rememberFocus(key) {
    const element = document.activeElement;
    rememberedFocus.set(key, { element, description: describeCell(element) });
}

export function restoreFocus(key) {
    const entry = rememberedFocus.get(key);
    rememberedFocus.delete(key);

    if (!entry)
        return;

    focusWithRetry(entry, 6);
}

// The dialog that just closed makes the rest of the page inert until its script lets go a moment later, and
// an inert element silently refuses focus - so the attempt is repeated until it takes.
function focusWithRetry(entry, attemptsLeft) {
    let target = entry.element && document.contains(entry.element) ? entry.element : findCell(entry.description);

    if (target) {
        target.focus({ preventScroll: true });

        if (document.activeElement === target)
            return;
    }

    if (attemptsLeft > 1)
        setTimeout(() => focusWithRetry(entry, attemptsLeft - 1), 80);
}

export function measureElement(element) {
    if (!element)
        return null;

    const box = element.getBoundingClientRect();
    return [box.width, box.height, document.documentElement.clientWidth, document.documentElement.clientHeight];
}

// The box of the per-row more button that a click landed on, so the menu can open beside the button
// instead of on the (possibly stale) pointer position. A keyboard-driven click reports no usable pointer
// position, so the focused element is the fallback.
export function getRowMenuButtonRect(clientX, clientY) {
    const hit = document.elementFromPoint(clientX, clientY);
    const button = (hit && hit.closest('.base-list-row-menu'))
        || (document.activeElement && document.activeElement.closest('.base-list-row-menu'));

    if (!button)
        return null;

    const box = button.getBoundingClientRect();
    return [box.left, box.top, box.right, box.bottom];
}

const grids = new WeakMap();

function isNearEnd(grid) {
    const remaining = grid.scrollHeight - grid.scrollTop - grid.clientHeight;
    return remaining <= Math.max(160, grid.clientHeight * 0.25);
}

function requestMoreIfNeeded(state) {
    if (!state.hasMore || state.requesting || !isNearEnd(state.grid))
        return;

    state.requesting = true;
    state.dotNetRef.invokeMethodAsync('OnGridNearEndAsync')
        .catch(() => { })
        .finally(() => { state.requesting = false; });
}

// A data grid is one Tab stop: FluentUI marks every cell tabindex=0, which makes a list of n rows cost
// n x columns Tab presses to get past. The cell that has focus (or the first one) stays in the Tab order and
// the arrow keys, which FluentUI handles, move between the others.
function applyRovingTabindex(state) {
    const cells = state.grid.querySelectorAll('td[role="gridcell"]');

    if (!state.current || !state.grid.contains(state.current))
        state.current = cells.length > 0 ? cells[0] : null;

    for (const cell of cells) {
        const wanted = cell === state.current ? '0' : '-1';
        if (cell.getAttribute('tabindex') !== wanted)
            cell.setAttribute('tabindex', wanted);
    }
}

export function observeGrid(grid, dotNetRef, restoreFocusKey) {
    if (!grid || grids.has(grid))
        return;

    const state = { grid, dotNetRef, hasMore: false, requesting: false, current: null, scheduled: false, pendingRestoreKey: restoreFocusKey || null };

    const schedule = () => {
        if (state.scheduled)
            return;

        state.scheduled = true;
        requestAnimationFrame(() => {
            state.scheduled = false;
            applyRovingTabindex(state);
        });
    };

    state.onScroll = () => requestMoreIfNeeded(state);

    state.onFocusIn = event => {
        const cell = event.target && event.target.closest ? event.target.closest('td[role="gridcell"]') : null;

        if (cell && cell !== state.current) {
            const previous = state.current;
            state.current = cell;
            cell.setAttribute('tabindex', '0');

            if (previous && previous !== cell)
                previous.setAttribute('tabindex', '-1');
        }
    };

    state.observer = new MutationObserver(schedule);
    state.observer.observe(grid, { childList: true, subtree: true });

    grid.addEventListener('scroll', state.onScroll, { passive: true });
    grid.addEventListener('focusin', state.onFocusIn);
    grids.set(grid, state);

    schedule();
}

export function updateGrid(grid, hasMore) {
    const state = grids.get(grid);
    if (!state)
        return;

    state.hasMore = hasMore;

    if (state.pendingRestoreKey) {
        const key = state.pendingRestoreKey;
        state.pendingRestoreKey = null;
        requestAnimationFrame(() => restoreFocus(key));
    }
}

export function disposeGrid(grid) {
    const state = grids.get(grid);
    if (!state)
        return;

    state.observer.disconnect();
    grid.removeEventListener('scroll', state.onScroll);
    grid.removeEventListener('focusin', state.onFocusIn);
    grids.delete(grid);
}