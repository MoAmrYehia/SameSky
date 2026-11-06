// SameSky service worker: Web Push only. Payloads are generic by design (no captions, places or transcripts).
self.addEventListener('push', (event) => {
  let data = { title: 'SameSky', body: 'A shared moment is waiting.' };
  try {
    if (event.data) data = { ...data, ...event.data.json() };
  } catch {
    /* keep the generic text */
  }
  event.waitUntil(self.registration.showNotification(data.title, { body: data.body, icon: '/icon.svg', badge: '/icon.svg', tag: data.kind || 'samesky' }));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      const existing = clients.find((c) => new URL(c.url).origin === self.location.origin);
      if (existing) return existing.focus();
      return self.clients.openWindow('/#/inbox');
    }),
  );
});
