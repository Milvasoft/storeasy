/* global self, clients, URL */
// storeasy browser push worker. It only shows push notifications and opens the page they point at: it does not cache,
// does not intercept requests and is registered under its own scope (/push/), so the dev mock's worker at / keeps
// answering the API. The payload is the API's WebPushSender JSON: { title, body, url, kind, notificationId }.

self.addEventListener('install', () => {
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim())
})

self.addEventListener('push', (event) => {
  let payload = {}
  try {
    payload = event.data ? event.data.json() : {}
  } catch {
    payload = { body: event.data ? event.data.text() : '' }
  }
  const title = payload.title || 'storeasy'
  event.waitUntil(
    self.registration.showNotification(title, {
      body: payload.body || '',
      icon: '/favicon.svg',
      badge: '/favicon.svg',
      tag: payload.notificationId ? `storeasy-${payload.notificationId}` : payload.kind || undefined,
      data: { url: payload.url || '/' },
    }),
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const target = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin)
  // Only addresses of this site are opened.
  const url = target.origin === self.location.origin ? target.href : self.location.origin + '/'
  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      for (const client of windows) {
        if (client.url === url && 'focus' in client) {
          return client.focus()
        }
      }
      return clients.openWindow(url)
    }),
  )
})
