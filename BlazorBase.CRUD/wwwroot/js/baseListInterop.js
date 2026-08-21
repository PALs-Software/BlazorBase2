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
const rememberedFocus = new Map();

export function rememberFocus(key) {
    rememberedFocus.set(key, document.activeElement);
}

export function restoreFocus(key) {
    const target = rememberedFocus.get(key);
    rememberedFocus.delete(key);

    if (!target || !document.contains(target))
        return;

    target.focus();
}
