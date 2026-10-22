const toolbars = new WeakMap();

// The toolbar lives in a header row next to other controls. Its declared actions stay inline while the whole row
// fits on one line; otherwise they collapse into the "more actions" menu. The natural width of the inline actions
// is measured even while they are hidden (they stay laid out, but invisible and out of the flow), so the decision
// never depends on the state it decides about.
function fit(state) {
    const root = state.root;
    const container = root.parentElement;
    const inline = root.querySelector(':scope > .base-action-toolbar-inline');

    if (!container || !inline || container.clientWidth === 0)
        return;

    const natural = inline.offsetWidth;
    const style = getComputedStyle(container);
    const gap = parseFloat(style.columnGap) || 0;
    const inner = container.clientWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);

    let used = 0;
    let siblings = 0;

    for (const child of container.children) {
        if (child === root)
            continue;

        const position = getComputedStyle(child).position;
        if (child.offsetParent === null || position === 'absolute' || position === 'fixed')
            continue;

        used += child.offsetWidth;
        siblings++;
    }

    const collapsed = natural > 0 && used + natural + gap * siblings > inner + 0.5;
    const value = collapsed ? 'true' : 'false';

    if (root.dataset.collapsed !== value)
        root.dataset.collapsed = value;
}

function observeSiblings(state) {
    state.resizeObserver.disconnect();

    const container = state.root.parentElement;
    if (!container)
        return;

    state.resizeObserver.observe(container);

    for (const child of container.children)
        state.resizeObserver.observe(child);

    const inline = state.root.querySelector(':scope > .base-action-toolbar-inline');
    if (inline)
        state.resizeObserver.observe(inline);
}

function schedule(state) {
    if (state.frame)
        return;

    state.frame = requestAnimationFrame(() => {
        state.frame = 0;
        fit(state);
    });
}

export function observe(root) {
    if (!root || toolbars.has(root))
        return;

    const state = { root, frame: 0 };
    state.resizeObserver = new ResizeObserver(() => schedule(state));
    state.mutationObserver = new MutationObserver(() => {
        observeSiblings(state);
        schedule(state);
    });

    toolbars.set(root, state);
    observeSiblings(state);

    if (root.parentElement)
        state.mutationObserver.observe(root.parentElement, { childList: true });

    state.mutationObserver.observe(root, { childList: true });

    if (document.fonts && document.fonts.ready)
        document.fonts.ready.then(() => schedule(state));

    fit(state);
}

export function unobserve(root) {
    const state = toolbars.get(root);
    if (!state)
        return;

    state.resizeObserver.disconnect();
    state.mutationObserver.disconnect();

    if (state.frame)
        cancelAnimationFrame(state.frame);

    toolbars.delete(root);
}

function findTrigger(root, key) {
    return root ? [...root.querySelectorAll('[data-menu-trigger]')].find(element => element.dataset.menuTrigger === key) : null;
}

// The box of a menu's trigger in viewport pixels, and the size of the viewport, so the menu can be placed beside it.
export function measure(root, key) {
    const element = findTrigger(root, key);

    if (!element)
        return null;

    const box = element.getBoundingClientRect();
    return [box.left, box.top, box.right, box.bottom, document.documentElement.clientWidth, document.documentElement.clientHeight];
}

export function focusTrigger(root, key) {
    const wrapper = findTrigger(root, key);
    const button = wrapper && wrapper.querySelector('fluent-button, button');

    if (button)
        button.focus();
}

export function measureMenu(menu) {
    if (!menu)
        return null;

    const box = menu.getBoundingClientRect();
    return [box.width, box.height, document.documentElement.clientWidth, document.documentElement.clientHeight];
}

// Arrow keys, Home and End move between the entries of an open menu. Disabled entries stay reachable, so the reason
// of a blocked action can be read from the keyboard.
export function attachMenuKeys(menu) {
    if (!menu || menu.dataset.keysAttached)
        return;

    menu.dataset.keysAttached = 'true';
    menu.addEventListener('keydown', event => {
        const keys = ['ArrowDown', 'ArrowUp', 'Home', 'End'];
        if (!keys.includes(event.key))
            return;

        const items = [...menu.querySelectorAll('[role="menuitem"]')];
        if (items.length === 0)
            return;

        event.preventDefault();
        const current = items.indexOf(document.activeElement);
        let next;

        if (event.key === 'Home')
            next = 0;
        else if (event.key === 'End')
            next = items.length - 1;
        else if (event.key === 'ArrowDown')
            next = current < 0 ? 0 : (current + 1) % items.length;
        else
            next = current < 0 ? items.length - 1 : (current - 1 + items.length) % items.length;

        items[next].focus();
    });
}
