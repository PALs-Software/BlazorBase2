import hljs from '../lib/highlight/highlight.min.js';

const ThemeHref = '_content/BlazorBase.CRUD/lib/highlight/github-dark.min.css';

function ensureTheme() {
    if (document.querySelector(`link[data-hljs-theme]`))
        return;

    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = ThemeHref;
    link.setAttribute('data-hljs-theme', '');
    document.head.appendChild(link);
}

export function highlightAll(rootElementId, language) {
    const root = document.getElementById(rootElementId);
    if (!root)
        return;

    ensureTheme();

    const cells = root.querySelectorAll('[data-code]');
    cells.forEach(cell => {
        const text = cell.textContent;
        if (!text)
            return;

        let result;
        if (language) {
            try {
                result = hljs.highlight(text, { language, ignoreIllegals: true });
            } catch {
                result = hljs.highlightAuto(text);
            }
        } else {
            const lang = cell.getAttribute('data-lang') || '';
            if (lang) {
                try {
                    result = hljs.highlight(text, { language: lang, ignoreIllegals: true });
                } catch {
                    result = hljs.highlightAuto(text);
                }
            } else {
                result = hljs.highlightAuto(text);
            }
        }

        cell.innerHTML = result.value;
        cell.classList.add('hljs');
    });
}
