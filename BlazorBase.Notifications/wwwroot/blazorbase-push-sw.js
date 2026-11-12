async function notifyOpenClients() {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of clients) {
        client.postMessage({ type: 'blazorbase-notification' });
    }
}

const NAVIGATE_MESSAGE_TYPE = 'blazorbase-push-navigate';
const NAVIGATE_ACKNOWLEDGE_TIMEOUT_MS = 1000;

function requestInAppNavigation(client, absoluteUrl) {
    return new Promise((resolve) => {
        const channel = new MessageChannel();
        const timer = setTimeout(() => {
            channel.port1.onmessage = null;
            resolve(false);
        }, NAVIGATE_ACKNOWLEDGE_TIMEOUT_MS);

        channel.port1.onmessage = () => {
            clearTimeout(timer);
            resolve(true);
        };

        client.postMessage({ type: NAVIGATE_MESSAGE_TYPE, url: absoluteUrl }, [channel.port2]);
    });
}

async function focusOrOpen(targetUrl) {
    const absoluteUrl = new URL(targetUrl, self.location.origin).href;
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const focusableClients = clients.filter((client) => 'focus' in client);
    const client = focusableClients.find((candidate) => candidate.url === absoluteUrl) || focusableClients[0];

    if (!client) {
        return self.clients.openWindow ? self.clients.openWindow(absoluteUrl) : undefined;
    }

    await client.focus();

    if (client.url === absoluteUrl) {
        return undefined;
    }

    const acknowledged = await requestInAppNavigation(client, absoluteUrl);
    if (acknowledged || !('navigate' in client)) {
        return undefined;
    }

    return client.navigate(absoluteUrl);
}

self.addEventListener('push', (event) => {
    const data = event.data ? event.data.json() : {};
    const options = {
        body: data.body,
        icon: data.icon,
        badge: data.badge,
        tag: data.tag,
        data: { url: data.url || '/' },
    };
    event.waitUntil(Promise.all([
        self.registration.showNotification(data.title || '', options),
        notifyOpenClients(),
    ]));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    event.waitUntil(focusOrOpen(event.notification.data.url));
});
