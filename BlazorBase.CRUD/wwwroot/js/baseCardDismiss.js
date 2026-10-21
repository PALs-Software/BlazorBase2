import { isPopupOpen } from './baseSelectPopup.js';

const stack = [];
let counter = 0;

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
    const dialog = dialogElement && dialogElement.closest('fluent-dialog');
    if (!dialog)
        return null;

    const token = (++counter).toString();

    const onOverlayClick = (event) => {
        if (event.target === dialog)
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

const FocusableSelector = 'fluent-text-field, fluent-text-area, fluent-select, fluent-date-picker, fluent-switch, fluent-number-field, input, textarea, select, [contenteditable="true"]';

export function focusFirstError(root) {
    const message = root && root.querySelector('.base-field-errors');
    if (!message)
        return;

    const accordionItem = message.closest('fluent-accordion-item');
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

