import { useState } from 'react'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import { useNotifications } from './useNotifications'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

export function NotificationsPage() {
  const [unreadOnly, setUnreadOnly] = useState(false)
  const { items, loading, problem, reload, markAsRead, markingId, markProblem } = useNotifications(unreadOnly)

  function changeFilter(next: boolean) {
    // Show "Loading…" while the other list is fetched (the effect runs after this render).
    reload()
    setUnreadOnly(next)
  }

  return (
    <section>
      <h1>Notifications</h1>
      <div className="card">
        <label className="checkbox">
          <input type="checkbox" checked={unreadOnly} onChange={(e) => changeFilter(e.target.checked)} />
          Unread only
        </label>

        {loading && <p className="muted">Loading notifications…</p>}

        {!loading && problem && (
          <>
            <ProblemDetailsAlert problem={problem} />
            <button type="button" onClick={reload}>Try again</button>
          </>
        )}

        {!loading && !problem && items.length === 0 && (
          <p className="muted">
            {unreadOnly ? 'No unread notifications.' : 'No notifications yet. You will see money you receive here, live.'}
          </p>
        )}

        {!loading && !problem && items.length > 0 && (
          <>
            <ProblemDetailsAlert problem={markProblem} />
            <ul className="notification-list">
              {items.map((n) => (
                <li key={n.id} className={n.readAt ? 'read' : 'unread'}>
                  <div>
                    <strong>{formatMoney(n.amount, n.currency)}</strong> received from <strong>{n.fromUsername}</strong>
                    <div className="muted">{dateFormat.format(new Date(n.occurredAt))}</div>
                  </div>
                  {n.readAt ? (
                    <span className="muted">Read</span>
                  ) : (
                    <button type="button" onClick={() => void markAsRead(n.id)} disabled={markingId === n.id}>
                      {markingId === n.id ? 'Marking…' : 'Mark as read'}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
    </section>
  )
}
