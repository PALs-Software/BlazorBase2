const stack = [];
let counter = 0;
let replayingEscape = false;

const FIELD_SELECTOR = [
    'input:not([type="hidden"])',
    'textarea',
    'select',
    '[slot="input"]',
    '[slot="control"]',
    '[contenteditable="true"]'
].join(',');

const ACTION_SELECTOR = 'button, [role="button"], a[href], [tabindex]:not([tabindex="-1"])';

function isFocusHost(element) {
    return !!element.shadowRoot && element.shadowRoot.delegatesFocus === true;
}

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

    for (const action of root.querySelectorAll('*')) {
        if ((action.matches(ACTION_SELECTOR) || isFocusHost(action)) && isUsable(action) && !action.classList.contains('base-card-close'))
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
 * Moves focus into the dialog it is rendered in and, optionally, commits a half-typed field when Escape is
 * pressed so a dirty check sees it. The dialog itself is the native modal of the Fluent dialog: it makes the page
 * behind it inert, closes on Escape and hands focus back to the opener.
 * @param {HTMLElement} root element rendered inside the dialog by DialogBehavior
 * @param {{initialFocus: boolean, commitOnEscape: boolean}} options
 * @returns {string|null} token to pass to unregister
 */
export function register(root, options) {
    const dialog = root && root.closest('.base-dialog');
    if (!dialog)
        return null;

    const token = (++counter).toString();

    if (stack.length === 0)
        window.addEventListener('keydown', onKeyDown, true);

    stack.push({ token, root, commitOnEscape: options.commitOnEscape });

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

    stack.splice(index, 1);

    if (stack.length === 0)
        window.removeEventListener('keydown', onKeyDown, true);
}
