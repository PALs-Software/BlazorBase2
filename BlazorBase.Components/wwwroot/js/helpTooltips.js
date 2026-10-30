const hoverDelay = 400;
const hideDelay = 180;
const maxAncestorLevels = 3;
const maxLabelLength = 80;
const maxTextGuard = 200;
const emptyIndexLifetime = 60000;
const tipId = 'base-help-tip';
const tipOffset = 6;
const edgeMargin = 8;
const allowedTags = new Set(['STRONG', 'B', 'EM', 'I', 'CODE', 'DEL']);
const excludedSelector = '.base-help-panel, .base-help-tip, .base-header, .base-nav-host';
const controlSelector = 'input, textarea, select, option, [slot="input"], [slot="control"]';

let state = null;

/**
 * Turns the field tooltips on: delegated listeners on the document show a small popover with the first sentence of
 * the help entry whose label equals the text of the hovered (or keyboard-focused) element, plus a link that opens the
 * help at that entry. The tips of the current page are requested from the component once per page, the first time the
 * pointer rests on a short text. Calling it again starts over with the new options. Nothing happens on touch input.
 */
export function enableFieldTooltips(reference, options) {
    disableFieldTooltips();

    if (!reference) {
        return;
    }

    state = {
        reference,
        moreLabel: options?.moreLabel || 'More in help',
        scopeSelector: options?.scopeSelector || '#base-main',
        indexPath: null,
        indexPromise: null,
        indexIsEmpty: true,
        indexLoadedAt: 0,
        pending: null,
        anchor: null,
        entry: null,
        describedBy: null,
        tip: null,
        textElement: null,
        linkElement: null,
        showTimer: 0,
        hideTimer: 0,
        usingKeyboard: false
    };

    document.addEventListener('pointerover', onPointerOver, true);
    document.addEventListener('pointerout', onPointerOut, true);
    document.addEventListener('pointerdown', onPointerDown, true);
    document.addEventListener('focusin', onFocusIn, true);
    document.addEventListener('focusout', onFocusOut, true);
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('scroll', onScrollOrResize, { capture: true, passive: true });
    window.addEventListener('resize', onScrollOrResize);
}

/** Removes the listeners and the popover. */
export function disableFieldTooltips() {
    document.removeEventListener('pointerover', onPointerOver, true);
    document.removeEventListener('pointerout', onPointerOut, true);
    document.removeEventListener('pointerdown', onPointerDown, true);
    document.removeEventListener('focusin', onFocusIn, true);
    document.removeEventListener('focusout', onFocusOut, true);
    document.removeEventListener('keydown', onKeyDown, true);
    document.removeEventListener('scroll', onScrollOrResize, { capture: true });
    window.removeEventListener('resize', onScrollOrResize);

    if (state === null) {
        return;
    }

    cancelPending(state);
    hideTip(state);
    state.tip?.remove();
    state = null;
}

/** Hides the popover, for example when the page changes. */
export function hideFieldTooltip() {
    if (state === null) {
        return;
    }

    cancelPending(state);
    hideTip(state);
}

function normalize(text) {
    return (text ?? '').replace(/\s+/g, ' ').trim().replace(/[\s:*]+$/, '').toLowerCase();
}

function isControl(element) {
    return element.matches(controlSelector);
}

function isInScope(element) {
    return element.closest(state.scopeSelector) !== null && element.closest(excludedSelector) === null;
}

function textOf(element) {
    if (isControl(element) || (element.textContent ?? '').length > maxTextGuard || element.querySelector('select, textarea')) {
        return '';
    }

    return element.innerText ?? element.textContent ?? '';
}

function looksLikeLabel(start) {
    let element = start;

    for (let level = 0; element && level <= maxAncestorLevels; level++, element = element.parentElement) {
        if (!isInScope(element) || isControl(element)) {
            return false;
        }

        const key = normalize(textOf(element));

        if (key.length > 0 && key.length <= maxLabelLength) {
            return true;
        }
    }

    return false;
}

function findMatch(start, index) {
    let element = start;

    for (let level = 0; element && level <= maxAncestorLevels; level++, element = element.parentElement) {
        if (!isInScope(element) || isControl(element)) {
            return null;
        }

        const entry = index.get(normalize(textOf(element)));

        if (entry) {
            return { element, entry };
        }
    }

    return null;
}

function findFocusMatch(target, index) {
    const host = target.closest(controlSelector);

    if (!host) {
        return findMatch(target, index);
    }

    const texts = [...(host.labels ?? [])].map(label => label.innerText ?? label.textContent);
    const slotted = host.querySelector('label');

    if (slotted) {
        texts.push(slotted.textContent);
    }

    texts.push(host.getAttribute('aria-label'), host.getAttribute('label'));

    for (const text of texts) {
        const entry = index.get(normalize(text));

        if (entry) {
            return { element: host, entry };
        }
    }

    return null;
}

function ensureIndex() {
    const current = state;
    const path = location.pathname;
    const expired = current.indexIsEmpty && Date.now() - current.indexLoadedAt > emptyIndexLifetime;

    if (current.indexPromise !== null && current.indexPath === path && !expired) {
        return current.indexPromise;
    }

    current.indexPath = path;
    current.indexIsEmpty = true;
    current.indexLoadedAt = Date.now();
    current.indexPromise = loadIndex(current);

    return current.indexPromise;
}

async function loadIndex(current) {
    const index = new Map();

    try {
        const data = await current.reference.invokeMethodAsync('GetFieldIndexAsync');

        for (const tip of data?.tips ?? []) {
            if (!index.has(tip.key)) {
                index.set(tip.key, { html: tip.html, anchor: tip.anchor, topic: data.topic });
            }
        }
    } catch {
        return new Map();
    }

    current.indexIsEmpty = index.size === 0;
    current.indexLoadedAt = Date.now();

    return index;
}

function begin(current, target, byFocus) {
    cancelPending(current);

    if (!isInScope(target) || (!byFocus && (isControl(target) || !looksLikeLabel(target)))) {
        return;
    }

    current.pending = target;
    const loading = ensureIndex();

    current.showTimer = setTimeout(async () => {
        const index = await loading;

        if (state !== current || current.pending !== target || !target.isConnected) {
            return;
        }

        const match = byFocus ? findFocusMatch(target, index) : findMatch(target, index);

        if (match) {
            show(current, match);
        }
    }, hoverDelay);
}

function cancelPending(current) {
    clearTimeout(current.showTimer);
    current.showTimer = 0;
    current.pending = null;
}

function scheduleHide(current) {
    clearTimeout(current.hideTimer);
    current.hideTimer = setTimeout(() => hideTip(current), hideDelay);
}

function cancelHide(current) {
    clearTimeout(current.hideTimer);
    current.hideTimer = 0;
}

function hideTip(current) {
    cancelHide(current);

    if (current.tip) {
        current.tip.hidden = true;
    }

    if (current.anchor) {
        if (current.describedBy === null) {
            current.anchor.removeAttribute('aria-describedby');
        } else {
            current.anchor.setAttribute('aria-describedby', current.describedBy);
        }
    }

    current.anchor = null;
    current.entry = null;
    current.describedBy = null;
}

function ensureTip(current) {
    if (current.tip) {
        return current.tip;
    }

    const tip = document.createElement('div');
    tip.id = tipId;
    tip.className = 'base-help-tip';
    tip.setAttribute('role', 'note');
    tip.hidden = true;

    const text = document.createElement('div');
    text.className = 'base-help-tip-text';

    const link = document.createElement('a');
    link.className = 'base-help-tip-link';
    link.href = '#help';
    link.addEventListener('click', event => openHelp(current, event));

    tip.append(text, link);
    tip.addEventListener('pointerenter', () => cancelHide(current));
    tip.addEventListener('pointerleave', () => scheduleHide(current));
    document.body.append(tip);

    current.tip = tip;
    current.textElement = text;
    current.linkElement = link;

    return tip;
}

function sanitizeInline(html) {
    const template = document.createElement('template');
    template.innerHTML = html ?? '';

    const clean = node => {
        for (const child of [...node.childNodes]) {
            if (child.nodeType === Node.TEXT_NODE) {
                continue;
            }

            if (child.nodeType !== Node.ELEMENT_NODE || !allowedTags.has(child.tagName)) {
                child.replaceWith(document.createTextNode(child.textContent ?? ''));
                continue;
            }

            for (const attribute of [...child.attributes]) {
                child.removeAttribute(attribute.name);
            }

            clean(child);
        }
    };

    clean(template.content);

    return template.content;
}

function show(current, match) {
    cancelHide(current);

    if (current.anchor && current.anchor !== match.element) {
        hideTip(current);
    }

    const tip = ensureTip(current);
    current.textElement.replaceChildren(sanitizeInline(match.entry.html));
    current.linkElement.textContent = current.moreLabel;

    if (current.anchor !== match.element) {
        current.describedBy = match.element.getAttribute('aria-describedby');
        match.element.setAttribute('aria-describedby', tipId);
    }

    current.anchor = match.element;
    current.entry = match.entry;
    tip.hidden = false;
    position(tip, match.element);
}

function position(tip, element) {
    const rect = element.getBoundingClientRect();
    tip.style.left = '0px';
    tip.style.top = '0px';

    const width = tip.offsetWidth;
    const height = tip.offsetHeight;
    const left = Math.max(edgeMargin, Math.min(rect.left, window.innerWidth - width - edgeMargin));
    let top = rect.bottom + tipOffset;

    if (top + height > window.innerHeight - edgeMargin && rect.top - height - tipOffset >= edgeMargin) {
        top = rect.top - height - tipOffset;
    }

    tip.style.left = left + 'px';
    tip.style.top = top + 'px';
}

function openHelp(current, event) {
    event.preventDefault();

    const entry = current.entry;
    hideTip(current);

    if (entry) {
        current.reference.invokeMethodAsync('OpenHelpAsync', entry.topic, entry.anchor);
    }
}

function onPointerOver(event) {
    const current = state;

    if (current === null || event.pointerType === 'touch' || !(event.target instanceof Element)) {
        return;
    }

    const target = event.target;

    if (current.tip?.contains(target) || current.anchor?.contains(target)) {
        cancelHide(current);
        return;
    }

    if (current.pending?.contains(target)) {
        return;
    }

    if (current.anchor) {
        scheduleHide(current);
    }

    begin(current, target, false);
}

function onPointerOut(event) {
    const current = state;

    if (current === null || event.pointerType === 'touch') {
        return;
    }

    const to = event.relatedTarget;

    if (to instanceof Node && (current.tip?.contains(to) || current.anchor?.contains(to) || current.pending?.contains(to))) {
        return;
    }

    cancelPending(current);

    if (current.anchor) {
        scheduleHide(current);
    }
}

function onPointerDown(event) {
    const current = state;

    if (current === null) {
        return;
    }

    current.usingKeyboard = false;

    if (!(event.target instanceof Node && current.tip?.contains(event.target))) {
        cancelPending(current);
        hideTip(current);
    }
}

function onFocusIn(event) {
    const current = state;

    if (current === null || !(event.target instanceof Element)) {
        return;
    }

    if (current.tip?.contains(event.target)) {
        cancelHide(current);
        return;
    }

    if (current.usingKeyboard) {
        begin(current, event.target, true);
    }
}

function onFocusOut(event) {
    const current = state;

    if (current === null || !current.anchor) {
        return;
    }

    if (event.relatedTarget instanceof Node && current.tip?.contains(event.relatedTarget)) {
        return;
    }

    scheduleHide(current);
}

function onKeyDown(event) {
    const current = state;

    if (current === null) {
        return;
    }

    if (event.key === 'Escape') {
        cancelPending(current);
        hideTip(current);
        return;
    }

    if (event.key.length > 1 && event.key !== 'Tab') {
        return;
    }

    current.usingKeyboard = true;
}

function onScrollOrResize() {
    const current = state;

    if (current === null) {
        return;
    }

    cancelPending(current);
    hideTip(current);
}
