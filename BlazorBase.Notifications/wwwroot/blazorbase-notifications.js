function base64UrlToUint8Array(base64Url) {
    const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
    const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(base64);
    const bytes = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) {
        bytes[i] = raw.charCodeAt(i);
    }
    return bytes;
}

function toSubscriptionDto(subscription) {
    const json = subscription.toJSON();
    return {
        endpoint: json.endpoint,
        p256dh: json.keys.p256dh,
        auth: json.keys.auth,
        userAgent: navigator.userAgent,
    };
}

export function isSupported() {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

export function getPermission() {
    return 'Notification' in window ? Notification.permission : 'default';
}

export async function subscribe(vapidPublicKey) {
    if (Notification.permission === 'denied') {
        throw new Error('Notification permission was previously denied.');
    }

    if (Notification.permission === 'default') {
        const requested = await Notification.requestPermission();
        if (requested !== 'granted') {
            throw new Error('Notification permission was not granted.');
        }
    }

    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: base64UrlToUint8Array(vapidPublicKey),
    });
    return toSubscriptionDto(subscription);
}

export async function unsubscribe() {
    if (!('serviceWorker' in navigator)) {
        return false;
    }

    const registration = await navigator.serviceWorker.getRegistration();
    if (!registration) {
        return false;
    }

    const subscription = await registration.pushManager.getSubscription();
    return subscription ? await subscription.unsubscribe() : false;
}

export async function getCurrentEndpoint() {
    if (!('serviceWorker' in navigator)) {
        return null;
    }

    const registration = await navigator.serviceWorker.getRegistration();
    if (!registration) {
        return null;
    }

    const subscription = await registration.pushManager.getSubscription();
    return subscription ? subscription.endpoint : null;
}

const refreshListeners = new Map();

export function registerRefreshListener(listenerId, dotNetHelper) {
    const onVisible = () => {
        if (document.visibilityState === 'visible') {
            dotNetHelper.invokeMethodAsync('OnRefreshRequested');
        }
    };
    const onMessage = (event) => {
        if (event.data && event.data.type === 'blazorbase-notification') {
            dotNetHelper.invokeMethodAsync('OnRefreshRequested');
        }
    };

    document.addEventListener('visibilitychange', onVisible);
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('message', onMessage);
    }

    refreshListeners.set(listenerId, { onVisible, onMessage });
}

export function unregisterRefreshListener(listenerId) {
    const listener = refreshListeners.get(listenerId);
    if (!listener) {
        return;
    }

    document.removeEventListener('visibilitychange', listener.onVisible);
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.removeEventListener('message', listener.onMessage);
    }

    refreshListeners.delete(listenerId);
}
