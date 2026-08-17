const stack = [];
let counter = 0;

function onKeyDown(event) {
    if (event.key !== 'Escape')
        return;

    if (stack.length === 0)
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
