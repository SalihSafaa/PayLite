import { useCallback, useEffect, useState } from 'react'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'

export type WalletDto = components['schemas']['WalletDto']

/** Loads GET /api/wallets/me and exposes loading / error / data, plus a way to replace it after a change. */
export function useMyWallet() {
    const [wallet, setWallet] = useState<WalletDto | null>(null)
    const [loading, setLoading] = useState(true)
    const [problem, setProblem] = useState<ProblemDetails | null>(null)
    // Bumping this number re-runs the effect below ("Try again").
    const [attempt, setAttempt] = useState(0)

    useEffect(() => {
        // If the component unmounts (or retries) before the response arrives, ignore the stale response.
        let ignore = false

        walletApi
            .GET('/api/wallets/me')
            .then(({ data, error }) => {
                if (ignore) return
                if (error || !data) {
                    setProblem(toProblemDetails(error))
                } else {
                    setWallet(data)
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

    return { wallet, setWallet, loading, problem, reload }
}
