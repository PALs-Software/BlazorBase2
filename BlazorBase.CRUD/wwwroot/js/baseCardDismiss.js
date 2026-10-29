const stack = [];
let counter = 0;

function deepActiveElement() {
    let active = document.activeElement;

    while (active && active.shadowRoot && active.shadowRoot.activeElement)
        active = active.shadowRoot.activeElement;

    return active;
}

// A select with an open popup is the focused element and reports aria-expanded; the key that closes the popup must not also close the dialog.
function isPopupOpen() {
    const host = document.activeElement;
    const active = deepActiveElement();

    return (!!host && host.getAttribute('aria-expanded') === 'true') || (!!active && active.getAttribute('aria-expanded') === 'true');
}

function onKeyDown(event) {
    if (event.key !== 'Escape')
        return;

    if (stack.length === 0)
        return;

    if (isPopupOpen())
        return;

    const entry = stack[stack.length - 1];
    entry.dotNetRef.invokeMethodAsync('RequestCloseAsync');
}

export function register(dialogElement, dotNetRef) {
    const dialog = dialogElement && dialogElement.closest('.base-dialog');
    if (!dialog)
        return null;

    const token = (++counter).toString();

    const onOverlayClick = (event) => {
        if (event.target !== dialog)
            return;

        const inner = event.composedPath()[0];
        const box = inner && inner.getBoundingClientRect ? inner.getBoundingClientRect() : null;
        const insideBox = box && event.clientX >= box.left && event.clientX <= box.right && event.clientY >= box.top && event.clientY <= box.bottom;

        if (!insideBox)
            dotNetRef.invokeMethodAsync('RequestCloseAsync');
    };

    dialog.addEventListener('click', onOverlayClick);

    if (stack.length === 0)
        document.addEventListener('keydown', onKeyDown, true);

    stack.push({ token, dotNetRef, dialog, onOverlayClick });
    return token;
}

export function unregister(token) {
    const index = stack.findIndex(entry => entry.token === token);
    if (index === -1)
        return;

    const entry = stack[index];
    entry.dialog.removeEventListener('click', entry.onOverlayClick);
    stack.splice(index, 1);

    if (stack.length === 0)
        document.removeEventListener('keydown', onKeyDown, true);
}

const FocusableSelector = '[slot="input"], input, textarea, select, [contenteditable="true"]';

export function focusFirstError(root) {
    const message = root && root.querySelector('.base-field-errors');
    if (!message)
        return;

    const accordionItem = message.closest('.base-card-group');
    if (accordionItem && !accordionItem.expanded)
        accordionItem.expanded = true;

    const field = message.closest('.base-property-input');
    const control = field && field.querySelector(FocusableSelector);

    if (control) {
        control.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        control.focus({ preventScroll: true });
        return;
    }

    message.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
}

