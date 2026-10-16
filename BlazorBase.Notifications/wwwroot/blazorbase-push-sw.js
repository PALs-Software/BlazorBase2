async function notifyOpenClients() {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of clients) {
        client.postMessage({ type: 'blazorbase-notification' });
    }
}

async function focusOrOpen(targetUrl) {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of clients) {
        if (client.url === targetUrl && 'focus' in client) {
            return client.focus();
        }
    }

    for (const client of clients) {
        if (!('focus' in client)) {
            continue;
        }
        await client.focus();
        return 'navigate' in client ? client.navigate(targetUrl) : undefined;
    }

    return self.clients.openWindow ? self.clients.openWindow(targetUrl) : undefined;
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
