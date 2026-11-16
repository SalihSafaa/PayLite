import { useCallback, useEffect, useState } from 'react'
import { walletApi } from '../../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import type { TopUpRequest } from './topUps'

/** GET /api/wallets/me/top-ups: my top-up requests with their status, newest first. */
export function useMyTopUps() {
  const [data, setData] = useState<TopUpRequest[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let ignore = false
    walletApi
      .GET('/api/wallets/me/top-ups')
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
  }, [attempt])

  const reload = useCallback(() => {
    setLoading(true)
    setProblem(null)
    setAttempt((n) => n + 1)
  }, [])

  return { data, loading, problem, reload }
}
