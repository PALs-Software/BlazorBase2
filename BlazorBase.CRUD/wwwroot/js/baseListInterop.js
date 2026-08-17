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
