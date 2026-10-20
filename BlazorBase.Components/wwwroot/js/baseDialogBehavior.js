const stack = [];
let counter = 0;
let replayingEscape = false;
const focusHistory = document.activeElement ? [document.activeElement] : [];

// FluentUI hands focus back itself only when the dialog is closed through its own close call; Escape and an overlay
// click close it through the dialog service, which does not. So the element that had focus before a dialog opened is
// tracked here and put back when the dialog goes away and focus was left on nothing.
document.addEventListener('focusin', event => {
    if (focusHistory[focusHistory.length - 1] !== event.target) {
        focusHistory.push(event.target);

        if (focusHistory.length > 12)
            focusHistory.shift();
    }
}, true);

function findOpener(dialog) {
    for (let index = focusHistory.length - 1; index >= 0; index--) {
        const candidate = focusHistory[index];

        if (document.contains(candidate) && !dialog.contains(candidate))
            return candidate;
    }

    return null;
}

const FIELD_SELECTOR = [
    'input:not([type="hidden"])',
    'textarea',
    'select',
    'fluent-text-field',
    'fluent-text-area',
    'fluent-number-field',
    'fluent-select',
    'fluent-combobox',
    'fluent-checkbox',
    'fluent-switch',
    'fluent-radio',
    'fluent-search',
    '[contenteditable="true"]'
].join(',');

const ACTION_SELECTOR = 'button, fluent-button, a[href], [tabindex]:not([tabindex="-1"])';

const SKIPPED_TAGS = new Set(['SCRIPT', 'STYLE', 'LINK', 'TEMPLATE']);

function isUsable(element) {
    if (!element || element.disabled === true || element.hasAttribute('disabled') || element.hasAttribute('readonly'))
        return false;

    if (element.closest('[inert], [hidden]'))
        return false;

    return element.getClientRects().length > 0;
}

function findInitialFocusTarget(root) {
    const marked = root.querySelector('[data-autofocus]');
    if (isUsable(marked))
        return marked;

    for (const field of root.querySelectorAll(FIELD_SELECTOR)) {
        if (isUsable(field))
            return field;
    }

    for (const action of root.querySelectorAll(ACTION_SELECTOR)) {
        if (isUsable(action) && !action.classList.contains('base-card-close'))
            return action;
    }

    return null;
}

function deepActiveElement() {
    let active = document.activeElement;

    while (active && active.shadowRoot && active.shadowRoot.activeElement)
        active = active.shadowRoot.activeElement;

    return active;
}

function makeBackgroundInert(dialog) {
    const marked = [];
    let node = dialog;

    while (node && node.parentElement && node !== document.body) {
        for (const sibling of node.parentElement.children) {
            if (sibling === node || SKIPPED_TAGS.has(sibling.tagName) || sibling.hasAttribute('inert'))
                continue;

            sibling.setAttribute('inert', '');
            marked.push(sibling);
        }

        node = node.parentElement;
    }

    return marked;
}

function isExpanded(host, active) {
    return host.getAttribute('aria-expanded') === 'true' || (!!active && active.getAttribute('aria-expanded') === 'true');
}

function onKeyDown(event) {
    if (event.key !== 'Escape' || replayingEscape || stack.length === 0)
        return;

    const top = stack[stack.length - 1];
    const host = document.activeElement;
    const active = deepActiveElement();

    if (!host || !top.root.contains(host) || isExpanded(host, active))
        return;

    if (top.dismissOnEscape && typeof top.dialog.dismiss === 'function') {
        event.stopPropagation();
        event.preventDefault();
        top.dialog.dismiss();
        return;
    }

    if (!top.commitOnEscape || !active || !(active.matches('input, textarea') || active.isContentEditable))
        return;

    event.stopPropagation();
    event.preventDefault();

    active.blur();
    host.focus();

    setTimeout(() => {
        replayingEscape = true;
        try {
            const replay = new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true, cancelable: true, composed: true });
            (document.activeElement || document.body).dispatchEvent(replay);
        } finally {
            replayingEscape = false;
        }
    }, 40);
}
/**
 * Makes the dialog the only thing the keyboard and pointer can reach, moves focus into it, and (optionally)
 * commits a half-typed field when Escape is pressed so a dirty check sees it.
 * @param {HTMLElement} root element rendered inside the dialog by DialogBehavior
 * @param {{initialFocus: boolean, commitOnEscape: boolean, dismissOnEscape: boolean}} options
 * @returns {string|null} token to pass to unregister
 */
export function register(root, options) {
    const dialog = root && root.closest('fluent-dialog');
    if (!dialog)
        return null;

    const token = (++counter).toString();
    const marked = makeBackgroundInert(dialog);

    if (stack.length === 0)
        window.addEventListener('keydown', onKeyDown, true);

    const opener = findOpener(dialog);

    stack.push({ token, root, dialog, marked, opener, commitOnEscape: options.commitOnEscape, dismissOnEscape: options.dismissOnEscape });

    if (options.initialFocus) {
        setTimeout(() => {
            const active = document.activeElement;
            if (active && active !== document.body && root.contains(active) && active !== root)
                return;

            const target = findInitialFocusTarget(root);
            if (target)
                target.focus({ preventScroll: true });
        }, 80);
    }

    return token;
}

export function unregister(token) {
    const index = stack.findIndex(entry => entry.token === token);
    if (index === -1)
        return;

    const entry = stack[index];
    stack.splice(index, 1);

    for (const element of entry.marked) {
        if (!stack.some(other => other.marked.includes(element)))
            element.removeAttribute('inert');
    }

    if (stack.length === 0)
        window.removeEventListener('keydown', onKeyDown, true);

    setTimeout(() => giveFocusBack(entry.opener, 4), 120);
}

function giveFocusBack(opener, attemptsLeft) {
    const active = document.activeElement;
    const focusIsLost = !active || active === document.body || !document.contains(active);

    if (!opener || !document.contains(opener) || !focusIsLost)
        return;

    opener.focus({ preventScroll: true });

    if (document.activeElement !== opener && attemptsLeft > 1)
        setTimeout(() => giveFocusBack(opener, attemptsLeft - 1), 80);
}
