const widthKey = 'blazorbase-assistant-width';
const wideQuery = '(min-width: 1200px)';
const minWidth = 320;
const stickThreshold = 80;
const keyboardStep = 16;

let current = null;

function clampWidth(value) {
    const max = Math.max(minWidth, Math.min(720, Math.round(window.innerWidth * 0.6)));
    return Math.max(minWidth, Math.min(max, Math.round(value)));
}

function defaultWidth() {
    return clampWidth(Math.min(440, Math.max(360, window.innerWidth * 0.28)));
}

function readStoredWidth() {
    try {
        const stored = Number(window.localStorage.getItem(widthKey));
        return stored > 0 ? clampWidth(stored) : defaultWidth();
    } catch {
        return defaultWidth();
    }
}

function storeWidth(width) {
    try {
        window.localStorage.setItem(widthKey, String(width));
    } catch {
    }
}

function reserveSpace(pixels) {
    const host = document.getElementById('base-main')?.parentElement;

    if (host) {
        host.style.marginInlineEnd = pixels > 0 ? pixels + 'px' : '';
    }
}

function autosize(textarea) {
    textarea.style.height = 'auto';
    textarea.style.height = Math.min(textarea.scrollHeight, 160) + 'px';
}

export function attach(panel, handle, transcript, composer, reference) {
    detach();

    const query = window.matchMedia(wideQuery);
    let width = readStoredWidth();
    let stick = true;

    const apply = () => {
        panel.style.setProperty('--assistant-width', width + 'px');
        handle.setAttribute('aria-valuenow', String(width));
        reserveSpace(query.matches ? width : 0);
    };

    const onPointerDown = event => {
        handle.setPointerCapture(event.pointerId);
        handle.dataset.dragging = 'true';
    };

    const onPointerMove = event => {
        if (handle.dataset.dragging !== 'true') {
            return;
        }

        width = clampWidth(window.innerWidth - event.clientX);
        apply();
    };

    const onPointerUp = event => {
        if (handle.dataset.dragging !== 'true') {
            return;
        }

        handle.dataset.dragging = 'false';
        handle.releasePointerCapture(event.pointerId);
        storeWidth(width);
    };

    const onHandleKey = event => {
        const delta = event.key === 'ArrowLeft' ? keyboardStep : event.key === 'ArrowRight' ? -keyboardStep : 0;

        if (delta === 0) {
            return;
        }

        event.preventDefault();
        width = clampWidth(width + delta);
        apply();
        storeWidth(width);
    };

    const onScroll = () => {
        stick = transcript.scrollHeight - transcript.scrollTop - transcript.clientHeight < stickThreshold;
    };

    const onComposerKey = event => {
        if (event.key !== 'Enter' || event.shiftKey || event.isComposing) {
            return;
        }

        event.preventDefault();
        reference.invokeMethodAsync('SubmitFromKeyboardAsync', composer.value);
    };

    const onComposerInput = () => autosize(composer);
    const viewport = window.visualViewport;
    const onViewport = () => {
        const inset = viewport ? Math.max(0, window.innerHeight - viewport.height - viewport.offsetTop) : 0;
        panel.style.setProperty('--assistant-keyboard-inset', Math.round(inset) + 'px');
    };

    const onResize = () => {
        width = clampWidth(width);
        apply();
        onViewport();
    };

    handle.addEventListener('pointerdown', onPointerDown);
    handle.addEventListener('pointermove', onPointerMove);
    handle.addEventListener('pointerup', onPointerUp);
    handle.addEventListener('pointercancel', onPointerUp);
    handle.addEventListener('keydown', onHandleKey);
    transcript.addEventListener('scroll', onScroll, { passive: true });
    composer.addEventListener('keydown', onComposerKey);
    composer.addEventListener('input', onComposerInput);
    query.addEventListener('change', apply);
    window.addEventListener('resize', onResize);
    viewport?.addEventListener('resize', onViewport);
    viewport?.addEventListener('scroll', onViewport);
    apply();
    onViewport();
    autosize(composer);

    current = {
        scrollToEnd(force) {
            if (force || stick) {
                transcript.scrollTop = transcript.scrollHeight;
                stick = true;
            }
        },
        autosize() {
            autosize(composer);
        },
        dispose() {
            handle.removeEventListener('pointerdown', onPointerDown);
            handle.removeEventListener('pointermove', onPointerMove);
            handle.removeEventListener('pointerup', onPointerUp);
            handle.removeEventListener('pointercancel', onPointerUp);
            handle.removeEventListener('keydown', onHandleKey);
            transcript.removeEventListener('scroll', onScroll);
            composer.removeEventListener('keydown', onComposerKey);
            composer.removeEventListener('input', onComposerInput);
            query.removeEventListener('change', apply);
            window.removeEventListener('resize', onResize);
            viewport?.removeEventListener('resize', onViewport);
            viewport?.removeEventListener('scroll', onViewport);
            reserveSpace(0);
        }
    };
}

export function detach() {
    current?.dispose();
    current = null;
}

export function scrollToEnd(force) {
    current?.scrollToEnd(force);
}

export function resizeComposer() {
    current?.autosize();
}

export function documentTitle() {
    return document.title || '';
}
