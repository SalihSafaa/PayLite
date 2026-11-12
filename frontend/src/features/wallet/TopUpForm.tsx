import { useState, type FormEvent } from 'react'
import { walletApi } from '../../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import type { WalletDto } from './useMyWallet'

const MAX_TOP_UP = 1_000_000 // same as Wallet:MaxTopUpAmount on the server, which has the final say

export function TopUpForm({ onToppedUp }: { onToppedUp: (wallet: WalletDto) => void }) {
    const [amount, setAmount] = useState('')
    const [submitting, setSubmitting] = useState(false)
    const [problem, setProblem] = useState<ProblemDetails | null>(null)

    async function handleSubmit(event: FormEvent) {
        event.preventDefault()
        const value = Number(amount)
        if (!Number.isFinite(value) || value <= 0) {
            setProblem({ title: 'Enter an amount greater than zero.' })
            return
        }
        if (value > MAX_TOP_UP) {
            setProblem({ title: `A single top-up cannot exceed ${MAX_TOP_UP.toLocaleString()} IQD.` })
            return
        }

        setSubmitting(true)
        setProblem(null)
        try {
            const { data, error } = await walletApi.POST('/api/wallets/me/top-ups', { body: { amount: value } })
            if (error || !data) {
                setProblem(toProblemDetails(error))
                return
            }
            setAmount('')
            onToppedUp(data)
        } catch (err) {
            setProblem(toProblemDetails(err))
        } finally {
            setSubmitting(false)
        }
    }

    return (
        <form onSubmit={handleSubmit} noValidate>
            <div className="inline-form">
                <label>
                    Top up (IQD)
                    <input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="50000" />
                </label>
                <button type="submit" disabled={submitting}>{submitting ? 'Topping up…' : 'Top up'}</button>
            </div>
            <ProblemDetailsAlert problem={problem} />
        </form>
    )
}
