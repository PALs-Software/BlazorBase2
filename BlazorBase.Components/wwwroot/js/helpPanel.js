const highlightClass = 'help-anchor-highlight';
const highlightDuration = 2200;
const spyOffset = 24;
const scrollMargin = 12;

const states = new WeakMap();
let highlighted = null;
let highlightTimer = 0;

function prefersReducedMotion() {
    return window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;
}

function findById(body, id) {
    if (!body || !id) {
        return null;
    }

    return body.querySelector('#' + CSS.escape(id));
}

/**
 * Keeps the table of contents of the help panel in step with the scroll position of the content: the heading
 * that is at or above the top of the content is the current section, its text is shown in the toggle of the table
 * of contents and its entry is marked with aria-current. The listeners live on the content element and on the
 * window; binding again, or the content element going away, ends the previous binding.
 */
export function bindHelpToc(body, toc) {
    if (!body) {
        return;
    }

    states.get(body)?.dispose();
    states.delete(body);

    if (!toc) {
        return;
    }

    const links = [...toc.querySelectorAll('[data-help-toc]')];
    const label = toc.querySelector('.base-help-toc-current');
    let frame = 0;
    let active = null;

    const update = () => {
        frame = 0;

        if (!body.isConnected) {
            return;
        }

        const top = body.getBoundingClientRect().top;
        let current = null;

        for (const link of links) {
            const heading = findById(body, link.dataset.helpToc);

            if (heading && heading.getBoundingClientRect().top - top <= spyOffset) {
                current = link;
            }
        }

        const atEnd = body.scrollTop > 0 && body.scrollTop + body.clientHeight >= body.scrollHeight - 2;

        if (atEnd && links.length > 0) {
            current = links[links.length - 1];
        }

        if (current === active) {
            return;
        }

        active?.removeAttribute('aria-current');
        active = current;
        active?.setAttribute('aria-current', 'location');

        if (label) {
            label.textContent = active ? active.textContent.trim() : '';
        }
    };

    const schedule = () => {
        if (frame === 0) {
            frame = requestAnimationFrame(update);
        }
    };

    body.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', schedule);
    states.set(body, {
        dispose() {
            body.removeEventListener('scroll', schedule);
            window.removeEventListener('resize', schedule);

            if (frame !== 0) {
                cancelAnimationFrame(frame);
            }
        }
    });

    update();
}

/**
 * Scrolls the help content so the element with the given id (a heading or a field entry) is at its top and
 * highlights it for a moment. Returns false when the content has no such element.
 */
export function scrollToHelpAnchor(body, id) {
    const target = findById(body, id);

    if (!target) {
        return false;
    }

    const top = target.getBoundingClientRect().top - body.getBoundingClientRect().top + body.scrollTop - scrollMargin;
    body.scrollTo({ top: Math.max(0, top), behavior: prefersReducedMotion() ? 'auto' : 'smooth' });

    if (/^H[1-6]$/.test(target.tagName)) {
        target.tabIndex = -1;
        target.focus({ preventScroll: true });
    }

    highlight(target);

    return true;
}

function highlight(target) {
    clearTimeout(highlightTimer);
    highlighted?.classList.remove(highlightClass);
    void target.offsetWidth;
    target.classList.add(highlightClass);
    highlighted = target;
    highlightTimer = setTimeout(() => {
        target.classList.remove(highlightClass);

        if (highlighted === target) {
            highlighted = null;
        }
    }, highlightDuration);
}
