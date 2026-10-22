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

// The cell that carries the keyboard focus of a row: its open cell, otherwise its first data cell. The row menu
// button, the select circle and the other columns are not where the keyboard rests between two rows.
function rowFocusCell(row) {
    return row ? (row.querySelector('td.base-list-open-cell') || row.querySelector('td.base-list-cell')) : null;
}

// The element to put the keyboard back on after an overlay closes. The three-dot button of a row is not it: its
// menu is the overlay, and the keyboard belongs on the row the menu was opened for.
function resolveRememberedElement(element) {
    const button = element && element.closest ? element.closest('.base-list-row-menu') : null;
    const cell = button ? rowFocusCell(button.closest('tr')) : null;

    return cell || element;
}

export function rememberFocus(key) {
    const element = resolveRememberedElement(document.activeElement);
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
// an inert element silently refuses focus - so the attempt is repeated until it takes. A cell of a grid that
// gets focus back this way is held (see holdFocusOn), because the grid may be reloading and about to replace it.
function focusWithRetry(entry, attemptsLeft) {
    let target = entry.element && document.contains(entry.element) ? entry.element : findCell(entry.description);

    if (target) {
        target.focus({ preventScroll: true });

        if (document.activeElement === target) {
            holdFocusInItsGrid(target);
            return;
        }
    }

    if (attemptsLeft > 1)
        setTimeout(() => focusWithRetry(entry, attemptsLeft - 1), 80);
}

function holdFocusInItsGrid(element) {
    const grid = element.closest ? element.closest('.base-list-grid') : null;
    const state = grid ? grids.get(grid) : null;

    if (state)
        holdFocusOn(state, element);
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

// The list does not scroll by itself: the page does. The nearest ancestor that scrolls vertically is the
// scroll container (the layout's main area); a list outside any such container follows the document.
function findScrollParent(element) {
    for (let parent = element.parentElement; parent; parent = parent.parentElement) {
        const overflowY = getComputedStyle(parent).overflowY;

        if (overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay')
            return parent;
    }

    return null;
}

function viewportBox(scroller) {
    if (!scroller)
        return { top: 0, bottom: window.innerHeight };

    const box = scroller.getBoundingClientRect();
    return { top: box.top + scroller.clientTop, bottom: box.top + scroller.clientTop + scroller.clientHeight };
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

// A virtualized row is replaced when its data arrives (a placeholder row turns into the real one) or when the list
// reloads, and a focused element that is replaced drops the keyboard on the page. The cell that had focus is
// therefore remembered by position (row and column index) for a moment and focused again on the equivalent
// cell once the rows have been rendered again, unless the user has moved on in the meantime.
const heldFocusMilliseconds = 3000;

function holdFocusOn(state, element) {
    const description = describeCell(element);
    state.heldFocus = description ? { description, until: performance.now() + heldFocusMilliseconds } : null;
}

function restoreHeldFocus(state) {
    const held = state.heldFocus;
    if (!held)
        return;

    const active = document.activeElement;
    const lost = !active || active === document.body || active === document.documentElement;

    if (!lost || performance.now() > held.until) {
        state.heldFocus = null;
        return;
    }

    const target = findCell(held.description);

    if (!target)
        return;

    target.focus({ preventScroll: true });

    if (document.activeElement === target)
        state.heldFocus = null;
}

export function holdFocus(grid) {
    const state = grids.get(grid);
    if (!state)
        return;

    const active = document.activeElement;

    if (active && grid.contains(active))
        holdFocusOn(state, active);
}

// The rows are 48 px high (BaseList.razor, ItemSize); rows that are not rendered yet are found by that height.
const rowHeight = 48;
const pendingFocusMilliseconds = 3000;

function dataIndexOfRow(row) {
    const index = Number(row.getAttribute('aria-rowindex')) - 2;
    return Number.isInteger(index) ? index : -1;
}

function findDataRow(state, dataIndex) {
    return state.grid.querySelector(`tr[aria-rowindex="${dataIndex + 2}"]:not([row-type])`);
}

// Scrolls the page by the least amount that brings the box between the sticky header and the bottom of the view.
function revealBox(state, top, bottom) {
    const view = viewportBox(state.scroller);
    const headerCell = state.grid.querySelector('tr[row-type] th');
    const limit = view.top + (headerCell ? headerCell.offsetHeight : 0);
    let delta = 0;

    if (top < limit)
        delta = top - limit;
    else if (bottom > view.bottom)
        delta = bottom - view.bottom;

    if (delta === 0)
        return;

    if (state.scroller)
        state.scroller.scrollTop += delta;
    else
        window.scrollBy(0, delta);
}

function revealRowAt(state, dataIndex) {
    const body = state.grid.querySelector('tbody');

    if (!body)
        return;

    const top = body.getBoundingClientRect().top + dataIndex * rowHeight;
    revealBox(state, top, top + rowHeight);
}

// Puts the keyboard on the row with the given index: the keyboard and the list's current row are the same row, so the
// next key, Enter among them, acts on the row the selection shows. A row the virtualizer has not rendered yet is
// scrolled to and focused as soon as it exists.
function focusDataRow(state, dataIndex, renderedOnly) {
    const cell = rowFocusCell(findDataRow(state, dataIndex));

    if (!cell) {
        if (!renderedOnly) {
            state.pendingFocus = { index: dataIndex, until: performance.now() + pendingFocusMilliseconds };
            revealRowAt(state, dataIndex);
            setTimeout(() => applyPendingFocus(state), 80);
        }

        return false;
    }

    cell.focus({ preventScroll: true });

    if (document.activeElement !== cell)
        return false;

    holdFocusOn(state, cell);
    const box = cell.getBoundingClientRect();
    revealBox(state, box.top, box.bottom);
    return true;
}

function applyPendingFocus(state) {
    const pending = state.pendingFocus;

    if (!pending)
        return;

    if (performance.now() > pending.until || !state.grid.isConnected) {
        state.pendingFocus = null;
        return;
    }

    if (focusDataRow(state, pending.index, true)) {
        state.pendingFocus = null;
        return;
    }

    revealRowAt(state, pending.index);
    setTimeout(() => applyPendingFocus(state), 80);
}

// The grid never scrolls vertically, so the browser cannot keep its header on screen while the page scrolls
// whenever the grid has to scroll sideways (a scroll container swallows sticky positioning). A grid that
// fits its box therefore keeps plain CSS sticky ("fits"); a wider one scrolls inside its box ("scroll") and
// the header follows the page by a translation set here, with a pinned scrollbar mirroring the grid's own.
function updateStickyHeader(state) {
    const header = state.grid.querySelector('tr[row-type]');
    if (!header)
        return;

    let offset = 0;

    if (state.grid.dataset.mode === 'scroll') {
        const view = viewportBox(state.scroller);
        const box = state.grid.getBoundingClientRect();
        const cell = header.querySelector('th');
        const headerHeight = cell ? cell.offsetHeight : 0;
        offset = Math.max(0, Math.min(view.top - box.top, box.height - headerHeight));
    }

    const value = offset + 'px';

    if (header.style.getPropertyValue('--base-list-sticky-offset') !== value)
        header.style.setProperty('--base-list-sticky-offset', value);
}

function updateLayout(state) {
    const grid = state.grid;
    const table = grid.querySelector('table');

    if (table !== state.table) {
        if (state.table)
            state.resizeObserver.unobserve(state.table);

        state.table = table;
        state.readyChecks = 0;

        if (table)
            state.resizeObserver.observe(table);
    }

    const virtualizerReady = table !== null && table.querySelector('tbody > tr[aria-hidden="true"][style*="table-row"]') !== null;

    if (table !== null && !virtualizerReady && state.readyChecks < 40) {
        state.readyChecks++;
        setTimeout(() => updateLayout(state), 50);
    }

    const wanted = virtualizerReady && grid.scrollWidth > grid.clientWidth + 1 ? 'scroll' : 'fits';

    if (grid.dataset.mode !== wanted)
        grid.dataset.mode = wanted;

    if (state.proxyContent) {
        const width = grid.scrollWidth + 'px';

        if (state.proxyContent.style.width !== width)
            state.proxyContent.style.width = width;
    }

    const sticky = state.scroller ? -parseFloat(getComputedStyle(state.scroller).paddingTop) || 0 : 0;
    const stickyValue = sticky + 'px';

    if (grid.style.getPropertyValue('--base-list-sticky-top') !== stickyValue)
        grid.style.setProperty('--base-list-sticky-top', stickyValue);

    const stickyBottom = state.scroller ? -parseFloat(getComputedStyle(state.scroller).paddingBottom) || 0 : 0;
    const stickyBottomValue = stickyBottom + 'px';

    if (state.proxy && state.proxy.style.getPropertyValue('--base-list-sticky-bottom') !== stickyBottomValue)
        state.proxy.style.setProperty('--base-list-sticky-bottom', stickyBottomValue);

    const listRoot = grid.parentElement;

    if (listRoot && listRoot.style.getPropertyValue('--base-list-sticky-bottom') !== stickyBottomValue)
        listRoot.style.setProperty('--base-list-sticky-bottom', stickyBottomValue);

    updateStickyHeader(state);
}

export function observeGrid(grid, dotNetRef, restoreFocusKey) {
    if (!grid || grids.has(grid))
        return;

    const proxy = grid.nextElementSibling && grid.nextElementSibling.classList.contains('base-list-hscroll')
        ? grid.nextElementSibling
        : null;

    const state = {
        grid, dotNetRef, proxy, proxyContent: proxy ? proxy.firstElementChild : null, scroller: findScrollParent(grid),
        table: null, current: null, scheduled: false, pendingRestoreKey: restoreFocusKey || null, keyTicket: 0, pendingFocus: null
    };

    const schedule = () => {
        if (state.scheduled)
            return;

        state.scheduled = true;
        requestAnimationFrame(() => {
            state.scheduled = false;
            applyRovingTabindex(state);
            updateLayout(state);
            restoreHeldFocus(state);
            applyPendingFocus(state);
        });
    };

    state.onPageScroll = () => {
        updateStickyHeader(state);
    };

    state.onResize = () => {
        updateLayout(state);
    };

    state.onGridScroll = () => {
        if (state.proxy && Math.abs(state.proxy.scrollLeft - grid.scrollLeft) >= 1)
            state.proxy.scrollLeft = grid.scrollLeft;
    };

    state.onProxyScroll = () => {
        if (Math.abs(state.proxy.scrollLeft - grid.scrollLeft) >= 1)
            grid.scrollLeft = state.proxy.scrollLeft;
    };

    state.onFocusIn = event => {
        state.heldFocus = null;

        const cell = event.target && event.target.closest ? event.target.closest('td[role="gridcell"]') : null;

        if (cell && cell !== state.current) {
            const previous = state.current;
            state.current = cell;
            cell.setAttribute('tabindex', '0');

            if (previous && previous !== cell)
                previous.setAttribute('tabindex', '-1');
        }
    };

    state.onFocusOut = event => {
        const target = event.target;

        if (event.relatedTarget === null && target && target.classList && target.classList.contains('grid-cell-placeholder'))
            holdFocusOn(state, target);
    };

    state.onPointerDown = () => {
        state.heldFocus = null;
        state.pendingFocus = null;
    };

    // The keys the selection model reacts to are reported to the list together with the row the keyboard stands on. The
    // arrow keys move the keyboard here; Space and Ctrl+A would scroll the page or select its text.
    state.onKeyDown = event => {
        if (event.altKey || event.defaultPrevented)
            return;

        const cell = event.target;

        if (!cell || cell.tagName !== 'TD' || cell.getAttribute('role') !== 'gridcell')
            return;

        const row = cell.closest('tr');

        if (!row || row.hasAttribute('row-type'))
            return;

        if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
            event.preventDefault();
            state.suppressContextMenuUntil = performance.now() + 1000;
            const anchor = (row.querySelector('.base-list-row-menu') || cell).getBoundingClientRect();
            state.dotNetRef.invokeMethodAsync('OnGridContextMenuKeyAsync', anchor.left, anchor.top, anchor.right, anchor.bottom).catch(() => { });
            return;
        }

        const ctrl = event.ctrlKey || event.metaKey;
        const key = event.key;
        const selectAll = ctrl && (key === 'a' || key === 'A');
        const vertical = key === 'ArrowUp' || key === 'ArrowDown';
        const reported = selectAll || vertical || (!ctrl && (key === ' ' || key === 'Enter' || key === 'Escape'));

        if (!reported)
            return;

        if (selectAll || key === ' ')
            event.preventDefault();

        const rowIndex = dataIndexOfRow(row);

        if (!vertical) {
            state.dotNetRef.invokeMethodAsync('OnGridKeyPressedAsync', key, event.shiftKey, ctrl, rowIndex, -1).catch(() => { });
            return;
        }

        // The data grid would move the keyboard itself, one row behind what the list has selected by the time the list
        // has answered; the keyboard is moved here instead, at once, and the list is told which row it is on.
        event.preventDefault();
        event.stopPropagation();

        const wanted = rowIndex + (key === 'ArrowDown' ? 1 : -1);
        const moved = wanted >= 0 && focusDataRow(state, wanted, true);
        const ticket = ++state.keyTicket;

        state.dotNetRef.invokeMethodAsync('OnGridKeyPressedAsync', key, event.shiftKey, ctrl, rowIndex, moved ? wanted : -1)
            .then(final => {
                if (final >= 0 && (!moved || final !== wanted) && ticket === state.keyTicket)
                    focusDataRow(state, final, false);
            })
            .catch(() => { });
    };

    // A Shift+click selects a range, which the browser would also answer by extending a text selection over the
    // rows. While Shift is held the rows cannot be selected as text (BaseList.razor.css), and a selection that
    // exists is dropped; a double click, which opens the card, would highlight a word the same way.
    const setShift = held => {
        if (held)
            grid.dataset.shift = 'true';
        else
            delete grid.dataset.shift;
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

    state.onDoubleClick = event => {
        const row = event.target && event.target.closest ? event.target.closest('tr') : null;

        if (row && !row.hasAttribute('row-type'))
            window.getSelection()?.removeAllRanges();
    };

    state.onContextMenuCapture = event => {
        if (!state.suppressContextMenuUntil || performance.now() > state.suppressContextMenuUntil)
            return;

        state.suppressContextMenuUntil = 0;
        event.preventDefault();
        event.stopPropagation();
    };

    state.observer = new MutationObserver(schedule);
    state.observer.observe(grid, { childList: true, subtree: true });

    state.resizeObserver = new ResizeObserver(schedule);
    state.resizeObserver.observe(grid);

    state.scrollTarget = state.scroller || window;
    state.scrollTarget.addEventListener('scroll', state.onPageScroll, { passive: true });
    window.addEventListener('resize', state.onResize, { passive: true });
    grid.addEventListener('scroll', state.onGridScroll, { passive: true });
    grid.addEventListener('focusin', state.onFocusIn);
    grid.addEventListener('focusout', state.onFocusOut);
    document.addEventListener('pointerdown', state.onPointerDown, true);
    grid.addEventListener('keydown', state.onKeyDown, true);
    grid.addEventListener('contextmenu', state.onContextMenuCapture, true);
    grid.addEventListener('mousedown', state.onMouseDown, true);
    grid.addEventListener('dblclick', state.onDoubleClick);
    document.addEventListener('keydown', state.onShiftKey);
    document.addEventListener('keyup', state.onShiftKey);
    window.addEventListener('blur', state.onWindowBlur);

    if (state.proxy)
        state.proxy.addEventListener('scroll', state.onProxyScroll, { passive: true });


    grids.set(grid, state);

    schedule();
}

export function scrollGridToTop(grid) {
    const state = grids.get(grid);
    if (!state)
        return;

    const delta = grid.getBoundingClientRect().top - viewportBox(state.scroller).top;

    if (delta >= 0)
        return;

    if (state.scroller)
        state.scroller.scrollTop += delta;
    else
        window.scrollBy(0, delta);
}

export function updateGrid(grid) {
    const state = grids.get(grid);
    if (!state)
        return;

    requestAnimationFrame(() => updateLayout(state));

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
    state.pendingFocus = null;
    state.resizeObserver.disconnect();
    state.scrollTarget.removeEventListener('scroll', state.onPageScroll);
    window.removeEventListener('resize', state.onResize);
    grid.removeEventListener('scroll', state.onGridScroll);
    grid.removeEventListener('focusin', state.onFocusIn);
    grid.removeEventListener('focusout', state.onFocusOut);
    document.removeEventListener('pointerdown', state.onPointerDown, true);
    grid.removeEventListener('keydown', state.onKeyDown, true);
    grid.removeEventListener('contextmenu', state.onContextMenuCapture, true);
    grid.removeEventListener('mousedown', state.onMouseDown, true);
    grid.removeEventListener('dblclick', state.onDoubleClick);
    document.removeEventListener('keydown', state.onShiftKey);
    document.removeEventListener('keyup', state.onShiftKey);
    window.removeEventListener('blur', state.onWindowBlur);

    if (state.proxy)
        state.proxy.removeEventListener('scroll', state.onProxyScroll);

    grids.delete(grid);
}