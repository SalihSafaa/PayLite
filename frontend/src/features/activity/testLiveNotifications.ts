import type { NotificationItem } from './liveNotifications'

/**
 * Test double for ./liveNotifications: no SignalR, but tests can "push" a notification
 * with push(...) and see how the components react. Use it with
 * vi.mock('./liveNotifications', () => import('./testLiveNotifications')).
 */
const received = new Set<(n: NotificationItem) => void>()
const changed = new Set<() => void>()

export const startLiveNotifications = (): void => {}
export const stopLiveNotifications = async (): Promise<void> => {}

export function onNotification(listener: (n: NotificationItem) => void): () => void {
  received.add(listener)
  return () => received.delete(listener)
}

export function notifyNotificationsChanged(): void {
  changed.forEach((l) => l())
}

export function onNotificationsChanged(listener: () => void): () => void {
  changed.add(listener)
  return () => changed.delete(listener)
}

/** Simulates the server pushing a notification over SignalR. */
export function push(notification: NotificationItem): void {
  received.forEach((l) => l(notification))
}

export function notification(id: number, amount: number, readAt: string | null = null): NotificationItem {
  return {
    id,
    transferId: 1000 + id,
    fromUsername: 'ali',
    amount,
    currency: 'IQD',
    occurredAt: '2026-11-16T09:15:00Z',
    readAt,
  }
}
