import { pushApi } from '@/lib/api/endpoints'

// The browser side of web push (RFC 8030 with VAPID, RFC 8292): a service worker of its own (/push-sw.js under the /push/
// scope, so the dev mock's worker at / is untouched) holds the subscription; the API gets the PushSubscription.toJSON()
// fields and encrypts every message to them (RFC 8291).

export const PUSH_WORKER_URL = '/push-sw.js'
export const PUSH_WORKER_SCOPE = '/push/'

export function isPushSupported(): boolean {
  return typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
}

export function notificationPermission(): NotificationPermission {
  return isPushSupported() ? Notification.permission : 'denied'
}

/** The VAPID public key (base64url) as the bytes PushManager.subscribe wants. */
export function applicationServerKey(base64url: string): Uint8Array<ArrayBuffer> {
  const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
  const raw = atob(padded)
  const bytes = new Uint8Array(new ArrayBuffer(raw.length))
  for (let index = 0; index < raw.length; index += 1) {
    bytes[index] = raw.charCodeAt(index)
  }
  return bytes
}

async function existingRegistration(): Promise<ServiceWorkerRegistration | undefined> {
  return navigator.serviceWorker.getRegistration(PUSH_WORKER_SCOPE)
}

/** This browser's subscription, or null (no worker, no subscription, no support). */
export async function currentSubscription(): Promise<PushSubscription | null> {
  if (!isPushSupported()) {
    return null
  }
  const registration = await existingRegistration()
  return (await registration?.pushManager.getSubscription()) ?? null
}

export class PushPermissionDeniedError extends Error {
  constructor() {
    super('Notifications are blocked for this site.')
    this.name = 'PushPermissionDeniedError'
  }
}

/** Asks for permission, subscribes this browser with the instance's key and hands the subscription to the API. */
export async function subscribeBrowser(publicKey: string): Promise<PushSubscription> {
  const permission = await Notification.requestPermission()
  if (permission !== 'granted') {
    throw new PushPermissionDeniedError()
  }
  const registration =
    (await existingRegistration()) ?? (await navigator.serviceWorker.register(PUSH_WORKER_URL, { scope: PUSH_WORKER_SCOPE }))
  const subscription =
    (await registration.pushManager.getSubscription()) ??
    (await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: applicationServerKey(publicKey) }))
  const json = subscription.toJSON()
  await pushApi.subscribe({
    Endpoint: subscription.endpoint,
    P256dh: json.keys?.p256dh ?? '',
    Auth: json.keys?.auth ?? '',
    UserAgent: navigator.userAgent,
  })
  return subscription
}

/** Removes this browser's subscription at the API and in the browser. */
export async function unsubscribeBrowser(subscription: PushSubscription): Promise<void> {
  await pushApi.unsubscribe({ Endpoint: subscription.endpoint })
  await subscription.unsubscribe()
}
