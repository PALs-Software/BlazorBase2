export function getUserAgent() {
    return navigator.userAgent || '';
}

export async function copyText(text) {
    try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch {
    }

    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    let copied = false;
    try {
        copied = document.execCommand('copy');
    } catch {
    }

    area.remove();
    return copied;
}

export function openMailto(uri, waitMilliseconds) {
    return new Promise(resolve => {
        let settled = false;
        const finish = value => {
            if (settled) {
                return;
            }

            settled = true;
            window.removeEventListener('blur', onLeave);
            window.removeEventListener('pagehide', onLeave);
            document.removeEventListener('visibilitychange', onVisibility);
            clearTimeout(timer);
            resolve(value);
        };
        const onLeave = () => finish(true);
        const onVisibility = () => {
            if (document.hidden) {
                finish(true);
            }
        };

        window.addEventListener('blur', onLeave);
        window.addEventListener('pagehide', onLeave);
        document.addEventListener('visibilitychange', onVisibility);
        const timer = setTimeout(() => finish(false), waitMilliseconds);

        const link = document.createElement('a');
        link.href = uri;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();
        link.remove();
    });
}
