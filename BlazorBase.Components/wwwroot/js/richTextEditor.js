const editors = new Map();

const AllowedLinkSchemes = ['http:', 'https:', 'mailto:'];

const AllowedImageSchemes = ['http:', 'https:'];

const MaxSanitizeDepth = 100;

const DropTags = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'LINK', 'META', 'BASE', 'FORM', 'INPUT', 'BUTTON', 'TEXTAREA', 'SELECT', 'SVG', 'MATH', 'TEMPLATE', 'NOSCRIPT']);

const AllowTags = new Set(['A', 'ABBR', 'B', 'BLOCKQUOTE', 'BR', 'CODE', 'DIV', 'EM', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'I', 'LI', 'OL', 'P', 'PRE', 'S', 'SPAN', 'STRIKE', 'STRONG', 'SUB', 'SUP', 'U', 'UL', 'HR', 'TABLE', 'THEAD', 'TBODY', 'TFOOT', 'TR', 'TD', 'TH', 'CAPTION', 'COL', 'COLGROUP', 'DL', 'DT', 'DD', 'FIGURE', 'FIGCAPTION', 'MARK', 'SMALL', 'IMG']);

function getEditor(elementId) {
    return editors.get(elementId);
}

function isAllowedLinkUrl(url) {
    try {
        const parsed = new URL(url);
        return AllowedLinkSchemes.includes(parsed.protocol.toLowerCase());
    } catch {
        return false;
    }
}

function sanitizeAnchorHref(anchor, value) {
    try {
        const parsed = new URL(value, document.baseURI);
        if (!AllowedLinkSchemes.includes(parsed.protocol.toLowerCase()))
            return;

        anchor.setAttribute('href', value);
        anchor.setAttribute('rel', 'noopener noreferrer');
    } catch {
    }
}

function sanitizeImageSrc(image, value) {
    try {
        const parsed = new URL(value, document.baseURI);
        if (!AllowedImageSchemes.includes(parsed.protocol.toLowerCase()))
            return;

        image.setAttribute('src', value);
    } catch {
    }
}

function appendSanitizedChildren(sourceParent, targetParent, depth) {
    if (depth > MaxSanitizeDepth)
        return;

    for (const child of Array.from(sourceParent.childNodes))
        appendSanitizedNode(child, targetParent, depth);
}

function appendSanitizedNode(node, targetParent, depth) {
    if (node.nodeType === Node.TEXT_NODE) {
        targetParent.appendChild(node.cloneNode());
        return;
    }

    if (node.nodeType !== Node.ELEMENT_NODE)
        return;

    const tag = node.tagName.toUpperCase();

    if (DropTags.has(tag))
        return;

    if (!AllowTags.has(tag)) {
        appendSanitizedChildren(node, targetParent, depth + 1);
        return;
    }

    const cleanElement = document.createElement(tag);

    if (tag === 'A' && node.hasAttribute('href'))
        sanitizeAnchorHref(cleanElement, node.getAttribute('href'));

    if (tag === 'IMG') {
        if (node.hasAttribute('src'))
            sanitizeImageSrc(cleanElement, node.getAttribute('src'));

        if (node.hasAttribute('alt'))
            cleanElement.setAttribute('alt', node.getAttribute('alt'));
    }

    if (node.hasAttribute('title'))
        cleanElement.setAttribute('title', node.getAttribute('title'));

    appendSanitizedChildren(node, cleanElement, depth + 1);
    targetParent.appendChild(cleanElement);
}

function sanitizeHtml(html) {
    const doc = new DOMParser().parseFromString(String(html ?? ''), 'text/html');
    const fragment = document.createDocumentFragment();

    appendSanitizedChildren(doc.body, fragment, 0);

    const container = document.createElement('div');
    container.appendChild(fragment);
    return container.innerHTML;
}

export function initEditor(elementId, dotNetRef, readOnly, placeholder, insertLinkPromptText) {
    const surface = document.getElementById(elementId);
    if (!surface)
        return;

    surface.contentEditable = readOnly ? 'false' : 'true';
    surface.setAttribute('role', 'textbox');
    surface.setAttribute('aria-multiline', 'true');

    const root = surface.closest('.rte-root');
    if (root)
        root.setAttribute('aria-disabled', readOnly ? 'true' : 'false');

    if (placeholder)
        surface.setAttribute('data-placeholder', placeholder);

    const promptText = insertLinkPromptText || 'Enter URL:';

    let debounceTimer = null;

    function notifyChange() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            const html = sanitizeHtml(surface.innerHTML);
            dotNetRef.invokeMethodAsync('OnContentChangedAsync', html);
        }, 200);
    }

    function handleInput() {
        notifyChange();
    }

    function handleToolbarMousedown(event) {
        const btn = event.target.closest('[data-cmd]');
        if (!btn)
            return;

        event.preventDefault();
    }

    function handleToolbarClick(event) {
        const btn = event.target.closest('[data-cmd]');
        if (!btn)
            return;

        const cmd = btn.getAttribute('data-cmd');

        if (cmd === 'createLink') {
            const url = window.prompt(promptText);
            if (url && isAllowedLinkUrl(url))
                document.execCommand('createLink', false, url);
        } else {
            document.execCommand(cmd, false, null);
        }

        surface.focus();
        notifyChange();
    }

    const toolbar = surface.previousElementSibling;
    if (toolbar && toolbar.classList.contains('rte-toolbar')) {
        toolbar.addEventListener('mousedown', handleToolbarMousedown);
        toolbar.addEventListener('click', handleToolbarClick);
    }

    surface.addEventListener('input', handleInput);

    editors.set(elementId, {
        surface,
        toolbar,
        handleInput,
        handleToolbarMousedown,
        handleToolbarClick,
        dotNetRef,
        debounceTimer: null
    });
}

export function setHtml(elementId, html) {
    const entry = getEditor(elementId);
    if (!entry)
        return;

    entry.surface.innerHTML = sanitizeHtml(html);
}

export function getHtml(elementId) {
    const entry = getEditor(elementId);
    if (!entry)
        return '';

    return entry.surface.innerHTML;
}

export function setReadOnly(elementId, readOnly) {
    const entry = getEditor(elementId);
    if (!entry)
        return;

    entry.surface.contentEditable = readOnly ? 'false' : 'true';

    const root = entry.surface.closest('.rte-root');
    if (root)
        root.setAttribute('aria-disabled', readOnly ? 'true' : 'false');
}

export function destroyEditor(elementId) {
    const entry = getEditor(elementId);
    if (!entry)
        return;

    clearTimeout(entry.debounceTimer);
    entry.surface.removeEventListener('input', entry.handleInput);

    if (entry.toolbar) {
        entry.toolbar.removeEventListener('mousedown', entry.handleToolbarMousedown);
        entry.toolbar.removeEventListener('click', entry.handleToolbarClick);
    }

    editors.delete(elementId);
}
