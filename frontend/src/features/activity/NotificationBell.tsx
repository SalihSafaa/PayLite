import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { activityApi } from '../../shared/api/clients'
import {
  onNotification,
  onNotificationsChanged,
  startLiveNotifications,
  stopLiveNotifications,
} from './liveNotifications'

/**
 * The live unread count in the top bar. It loads the count once, then:
 * +1 for every notification pushed over SignalR, and reloads when something is marked as read.
 * It also owns the SignalR connection: the bell is only rendered while someone is logged in.
 */
export function NotificationBell() {
  const [unread, setUnread] = useState<number | null>(null)

  const loadCount = useCallback(() => {
    activityApi
      .GET('/api/notifications', { params: { query: { unreadOnly: true, limit: 1 } } })
      .then(({ data }) => {
        if (data) setUnread(data.unreadCount)
      })
      .catch(() => {
        // The bell is not worth an error banner: it keeps the last count it had.
      })
  }, [])

  useEffect(() => {
    loadCount()
    startLiveNotifications()
    const offReceived = onNotification(() => setUnread((n) => (n ?? 0) + 1))
    const offChanged = onNotificationsChanged(loadCount)
    return () => {
      offReceived()
      offChanged()
      void stopLiveNotifications()
    }
  }, [loadCount])

  const label = unread ? `Notifications, ${unread} unread` : 'Notifications'

  return (
    <Link to="/notifications" className="bell" aria-label={label} title={label}>
      <span aria-hidden="true">🔔</span>
      {unread ? <span className="bell-count">{unread > 99 ? '99+' : unread}</span> : null}
    </Link>
  )
}
