/**
 * TODO (Owner B): live unread count. Load GET /api/notifications?unreadOnly=true, then
 * connect to /hubs/notifications with @microsoft/signalr, passing the in-memory token via
 * `accessTokenFactory: () => tokenStore.get() ?? ''`.
 */
export function NotificationBell() {
  return (
    <span className="bell" aria-label="Notifications">
      🔔
    </span>
  )
}
