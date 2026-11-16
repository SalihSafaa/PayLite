import { useCallback, useEffect, useState } from 'react'
import { activityApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/activity'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { onNotification } from './liveNotifications'

export type DailySummary = components['schemas']['DailySummaryDto']

/** Today as yyyy-MM-dd in Baghdad: the business day the server buckets payments by. */
export function todayInBaghdad(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baghdad' }).format(new Date())
}

/** GET /api/merchants/me/daily-summary?date=. When it shows today, a live payment reloads it. */
export function useDailySummary(date: string) {
  const [data, setData] = useState<DailySummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let ignore = false

    activityApi
      .GET('/api/merchants/me/daily-summary', { params: { query: { date } } })
      .then(({ data: result, error }) => {
        if (ignore) return
        if (error || !result) {
          setProblem(toProblemDetails(error))
        } else {
          setData(result)
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
  }, [date, attempt])

  // The pushed notification is committed before it's pushed, so the summary already includes it.
  useEffect(
    () =>
      onNotification(() => {
        if (date === todayInBaghdad()) setAttempt((n) => n + 1)
      }),
    [date],
  )

  const reload = useCallback(() => {
    setLoading(true)
    setProblem(null)
    setAttempt((n) => n + 1)
  }, [])

  return { data, loading, problem, reload }
}
