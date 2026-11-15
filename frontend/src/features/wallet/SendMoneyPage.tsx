import { useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'

type TransferResultDto = components['schemas']['TransferResultDto']

const MAX_TRANSFER = 10_000_000 // same as Wallet:MaxTransferAmount on the server, which has the final say
const IDEMPOTENCY_HEADER = 'Idempotency-Key' // keep in sync with Shared.Contracts/Http/CorrelationHeaders.cs

export function SendMoneyPage() {
    const [toUsername, setToUsername] = useState('')
    const [amount, setAmount] = useState('')
    const [submitting, setSubmitting] = useState(false)
    const [problem, setProblem] = useState<ProblemDetails | null>(null)
    const [sent, setSent] = useState<{ transfer: TransferResultDto; replayed: boolean } | null>(null)
    // One key per filled-in form, reused on every retry so the server never moves the money twice (ADR-0002).
    const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
    // A ref updates at once, so a fast double-click can't send before `submitting` re-renders the button.
    const inFlight = useRef(false)

    // Editing the form makes it a new transfer, so it gets a new key.
    function handleChange(setter: (value: string) => void, value: string) {
        setter(value)
        setIdempotencyKey(crypto.randomUUID())
        setSent(null)
    }

    async function handleSubmit(event: FormEvent) {
        event.preventDefault()
        if (inFlight.current) return

        const username = toUsername.trim()
        if (username.length < 3 || username.length > 50) {
            setProblem({ title: 'Recipient username must be 3 to 50 characters.' })
            return
        }
        if (!/^\d+$/.test(amount.trim())) {
            setProblem({ title: 'Amount must be a whole number of dinars.' })
            return
        }
        const value = Number(amount)
        if (value < 1) {
            setProblem({ title: 'Amount must be at least 1 IQD.' })
            return
        }
        if (value > MAX_TRANSFER) {
            setProblem({ title: `A single transfer cannot exceed ${formatMoney(MAX_TRANSFER)}.` })
            return
        }

        inFlight.current = true
        setSubmitting(true)
        setProblem(null)
        setSent(null)
        try {
            const { data, error, response } = await walletApi.POST('/api/transfers', {
                params: { header: { [IDEMPOTENCY_HEADER]: idempotencyKey } },
                body: { toUsername: username, amount: value },
            })
            if (error || !data) {
                // The key is kept, so pressing Send again is a real retry of the same transfer.
                setProblem(toProblemDetails(error))
                return
            }
            setSent({ transfer: data, replayed: response.headers.get('Idempotent-Replayed') === 'true' })
            setToUsername('')
            setAmount('')
            setIdempotencyKey(crypto.randomUUID())
        } catch (err) {
            // Network error: we don't know if the server got it. Retrying with the same key is safe.
            setProblem(toProblemDetails(err))
        } finally {
            inFlight.current = false
            setSubmitting(false)
        }
    }

    return (
        <section>
            <h1>Send money</h1>
            <div className="card">
                <form onSubmit={handleSubmit} noValidate className="stack">
                    <label>
                        Recipient username
                        <input value={toUsername} onChange={(e) => handleChange(setToUsername, e.target.value)} autoComplete="off" placeholder="bob" />
                    </label>
                    <label>
                        Amount (IQD)
                        <input inputMode="numeric" value={amount} onChange={(e) => handleChange(setAmount, e.target.value)} placeholder="25000" />
                    </label>
                    <div>
                        <button type="submit" disabled={submitting}>{submitting ? 'Sending…' : 'Send'}</button>
                    </div>
                </form>
                <ProblemDetailsAlert problem={problem} />
                {sent && (
                    <div role="status" className="success">
                        <strong>Sent {formatMoney(sent.transfer.amount, sent.transfer.currency)} to {sent.transfer.toUsername}.</strong>
                        <p>New balance: {formatMoney(sent.transfer.newBalance, sent.transfer.currency)}</p>
                        {sent.replayed && <p className="muted">This transfer was already done, so nothing was sent twice.</p>}
                        <Link to="/history">View history</Link>
                    </div>
                )}
            </div>
        </section>
    )
}
