import { useCallback, useEffect, useState } from 'react'
import { activityApi } from '../../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { notifyNotificationsChanged, onNotification, type NotificationItem } from './liveNotifications'

/** GET /api/notifications, plus live pushes prepended to the list and mark-as-read. */
export function useNotifications(unreadOnly: boolean) {
  const [items, setItems] = useState<NotificationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [markingId, setMarkingId] = useState<number | null>(null)
  const [markProblem, setMarkProblem] = useState<ProblemDetails | null>(null)
  // Bumping this number re-runs the effect below ("Try again").
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let ignore = false

    activityApi
      .GET('/api/notifications', { params: { query: { unreadOnly } } })
      .then(({ data, error }) => {
        if (ignore) return
        if (error || !data) {
          setProblem(toProblemDetails(error))
        } else {
          setItems(data.items)
          setProblem(null)
        }
      })
      .catch((err: unknown) => {
        if (!ignore) setProblem(toProblemDetails(err))
      })
      .finally(() => {
        if (!ignore) setLoading(false)
      })

    return () => {
      ignore = true
    }
  }, [unreadOnly, attempt])

  // A new notification arrives live: put it on top (it's unread, so it fits both filters).
  useEffect(
    () =>
      onNotification((notification) =>
        setItems((current) => (current.some((n) => n.id === notification.id) ? current : [notification, ...current])),
      ),
    [],
  )

  const reload = useCallback(() => {
    setLoading(true)
    setProblem(null)
    setAttempt((n) => n + 1)
  }, [])

  const markAsRead = useCallback(
    async (id: number) => {
      setMarkingId(id)
      setMarkProblem(null)
      try {
        const { data, error } = await activityApi.POST('/api/notifications/{id}/read', { params: { path: { id } } })
        if (error || !data) {
          setMarkProblem(toProblemDetails(error))
          return
        }
        setItems((current) =>
          unreadOnly ? current.filter((n) => n.id !== id) : current.map((n) => (n.id === id ? data : n)),
        )
        notifyNotificationsChanged()
      } catch (err) {
        setMarkProblem(toProblemDetails(err))
      } finally {
        setMarkingId(null)
      }
    },
    [unreadOnly],
  )

  return { items, loading, problem, reload, markAsRead, markingId, markProblem }
}
