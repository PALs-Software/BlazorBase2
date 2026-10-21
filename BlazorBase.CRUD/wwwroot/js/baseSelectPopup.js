const HOST_TAGS = new Set(['FLUENT-SELECT', 'FLUENT-COMBOBOX']);
const MARGIN = 8;
const GAP = 2;
const MIN_HEIGHT = 96;
const PREFERRED_BELOW = 280;
const MAX_WIDTH = 480;
const PLACED_PROPERTIES = ['position', 'top', 'left', 'right', 'bottom', 'inset', 'margin', 'width', 'min-width', 'max-width', 'height', 'max-height', 'z-index'];

const placements = new Map();

function listboxOf(host) {
    return host.shadowRoot ? host.shadowRoot.querySelector('[part="listbox"]') : null;
}

function supportsTopLayer() {
    return typeof HTMLElement !== 'undefined' && typeof HTMLElement.prototype.showPopover === 'function';
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), Math.max(min, max));
}

function place(host, listbox) {
    const anchor = host.getBoundingClientRect();
    const viewportWidth = document.documentElement.clientWidth;
    const viewportHeight = window.innerHeight;
    const maxWidth = Math.min(MAX_WIDTH, viewportWidth - 2 * MARGIN);
    const style = listbox.style;

    style.setProperty('position', 'fixed');
    style.setProperty('inset', 'auto');
    style.setProperty('margin', '0');
    style.setProperty('min-width', `${Math.round(Math.min(anchor.width, maxWidth))}px`);
    style.setProperty('max-width', `${Math.round(maxWidth)}px`);
    style.setProperty('width', 'max-content');
    style.setProperty('z-index', '2147483647');

    const border = listbox.offsetHeight - listbox.clientHeight;
    const natural = listbox.scrollHeight + border;
    const below = viewportHeight - anchor.bottom - MARGIN - GAP;
    const above = anchor.top - MARGIN - GAP;
    const placeBelow = below >= Math.min(natural, PREFERRED_BELOW) || below >= above;
    const height = Math.min(natural, Math.max(placeBelow ? below : above, MIN_HEIGHT));
    const top = placeBelow ? anchor.bottom + GAP : anchor.top - GAP - height;

    style.setProperty('max-height', `${Math.round(height)}px`);
    style.setProperty('top', `${Math.round(clamp(top, MARGIN, viewportHeight - height - MARGIN))}px`);
    style.setProperty('left', `${Math.round(clamp(anchor.left, MARGIN, viewportWidth - listbox.offsetWidth - MARGIN))}px`);
}

function show(host) {
    const listbox = listboxOf(host);
    if (!listbox || placements.has(host))
        return;

    listbox.setAttribute('popover', 'manual');

    try {
        listbox.showPopover();
    } catch {
        listbox.removeAttribute('popover');
        return;
    }

    const update = () => {
        if (!host.isConnected) {
            hide(host);
            return;
        }

        if (host.hasAttribute('open'))
            place(host, listbox);
    };

    const onScroll = event => {
        if (event.target !== listbox && !listbox.contains(event.target))
            update();
    };

    const resizeObserver = new ResizeObserver(() => requestAnimationFrame(update));
    resizeObserver.observe(listbox);
    window.addEventListener('resize', update);
    window.addEventListener('scroll', onScroll, true);
    placements.set(host, { listbox, resizeObserver, update, onScroll });

    update();
    requestAnimationFrame(() => requestAnimationFrame(update));
}

function hide(host) {
    const placement = placements.get(host);
    if (!placement)
        return;

    placements.delete(host);
    placement.resizeObserver.disconnect();
    window.removeEventListener('resize', placement.update);
    window.removeEventListener('scroll', placement.onScroll, true);

    for (const property of PLACED_PROPERTIES)
        placement.listbox.style.removeProperty(property);

    try {
        placement.listbox.hidePopover();
    } catch {
    }

    placement.listbox.removeAttribute('popover');
}

function onOpenChanged(host) {
    if (!HOST_TAGS.has(host.tagName))
        return;

    if (host.hasAttribute('open'))
        requestAnimationFrame(() => host.hasAttribute('open') && show(host));
    else
        hide(host);
}

/**
 * Whether a select or combobox popup is open right now, so a key that closes the popup is not also taken as a request
 * to close the dialog around it.
 * @returns {boolean}
 */
export function isPopupOpen() {
    return !!document.querySelector('fluent-select[open], fluent-combobox[open]');
}

/**
 * Lifts the popup of every FluentSelect and FluentCombobox into the top layer. Inside a card the popup is absolutely
 * positioned in the card body, which scrolls and clips, so it showed only a few rows or a single option. The popup is
 * placed fully inside the viewport, below the field when it fits and above it otherwise, and scrolls on its own when
 * it is longer than the space left.
 */
function install() {
    if (!supportsTopLayer() || window.__baseSelectPopupInstalled)
        return;

    window.__baseSelectPopupInstalled = true;

    new MutationObserver(records => {
        for (const record of records)
            onOpenChanged(record.target);
    }).observe(document.documentElement, { attributes: true, subtree: true, attributeFilter: ['open'] });
}

install();
