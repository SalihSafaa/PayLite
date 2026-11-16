import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { components } from '../../shared/api/generated/activity'
import { tokenStore } from '../../shared/auth/tokenStore'

export type NotificationItem = components['schemas']['NotificationDto']

// One SignalR connection for the whole app, owned by the bell (it's on screen whenever someone is
// logged in). Pages subscribe to it instead of opening their own connections.
//
// The token goes as ?access_token= because browsers can't set headers on a WebSocket. The Activity
// Service accepts it ONLY on /hubs/notifications (ADR-0003). It's read from memory on every (re)connect.

const HUB_PATH = '/hubs/notifications'
const METHOD = 'notification' // NotificationHub.NotificationMethod on the server

const received = new Set<(notification: NotificationItem) => void>()
const changed = new Set<() => void>()
let connection: HubConnection | null = null

export function startLiveNotifications(): void {
  if (connection) return
  connection = new HubConnectionBuilder()
    .withUrl(HUB_PATH, { accessTokenFactory: () => tokenStore.get() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
  connection.on(METHOD, (notification: NotificationItem) => {
    received.forEach((listener) => listener(notification))
  })
  // A failed start isn't fatal: the bell and the pages still load from the REST endpoint.
  connection.start().catch((err: unknown) => console.warn('Live notifications are unavailable', err))
}

export async function stopLiveNotifications(): Promise<void> {
  const current = connection
  connection = null
  await current?.stop()
}

/** Called for every notification pushed by the server. Returns the unsubscribe function. */
export function onNotification(listener: (notification: NotificationItem) => void): () => void {
  received.add(listener)
  return () => received.delete(listener)
}

/** Tell the bell to reload its count, e.g. after something was marked as read. */
export function notifyNotificationsChanged(): void {
  changed.forEach((listener) => listener())
}

export function onNotificationsChanged(listener: () => void): () => void {
  changed.add(listener)
  return () => changed.delete(listener)
}
