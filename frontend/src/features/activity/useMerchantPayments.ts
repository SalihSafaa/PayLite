import { useCallback, useEffect, useState } from 'react'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { onNotification } from './liveNotifications'

export type MerchantPayment = components['schemas']['MerchantPaymentDto']
export type MerchantPaymentsPage = components['schemas']['MerchantPaymentDtoPagedResponseDto']

/**
 * One page of GET /api/merchants/me/payments. Served by the Wallet API (the source of truth for
 * money); a live notification reloads the first page so a new payment shows up straight away.
 */
export function useMerchantPayments(page: number, pageSize: number) {
  const [data, setData] = useState<MerchantPaymentsPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let ignore = false

    walletApi
      .GET('/api/merchants/me/payments', { params: { query: { page, pageSize } } })
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
  }, [page, pageSize, attempt])

  useEffect(
    () =>
      onNotification(() => {
        if (page === 1) setAttempt((n) => n + 1)
      }),
    [page],
  )

  const reload = useCallback(() => {
    setLoading(true)
    setProblem(null)
    setAttempt((n) => n + 1)
  }, [])

  return { data, loading, problem, reload }
}
