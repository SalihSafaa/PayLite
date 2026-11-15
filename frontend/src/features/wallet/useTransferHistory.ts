import { useCallback, useEffect, useState } from 'react'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'

export type TransferHistoryItem = components['schemas']['TransferHistoryItemDto']
export type TransferHistoryPage = components['schemas']['TransferHistoryItemDtoPagedResponseDto']

/** Loads one page of GET /api/transfers. Changing page/pageSize or calling reload() fetches again. */
export function useTransferHistory(page: number, pageSize: number) {
    const [data, setData] = useState<TransferHistoryPage | null>(null)
    const [loading, setLoading] = useState(true)
    const [problem, setProblem] = useState<ProblemDetails | null>(null)
    // Bumping this number re-runs the effect below ("Try again").
    const [attempt, setAttempt] = useState(0)

    useEffect(() => {
        // If the page changes (or the component unmounts) before the response arrives, ignore the stale response.
        let ignore = false

        walletApi
            .GET('/api/transfers', { params: { query: { page, pageSize } } })
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

    const reload = useCallback(() => {
        setLoading(true)
        setProblem(null)
        setAttempt((n) => n + 1)
    }, [])

    return { data, loading, problem, reload }
}
