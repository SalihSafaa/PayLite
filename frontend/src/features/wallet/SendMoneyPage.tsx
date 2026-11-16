import { Alert, Anchor, Button, Card, Stack, Text, TextInput, Title } from '@mantine/core'
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
        <Stack maw={480}>
            <Title order={2}>Send money</Title>
            <Card withBorder radius="md" p="lg">
                <form onSubmit={handleSubmit} noValidate>
                    <Stack>
                        <TextInput label="Recipient username" value={toUsername} onChange={(e) => handleChange(setToUsername, e.currentTarget.value)} autoComplete="off" placeholder="bob" />
                        <TextInput label="Amount (IQD)" description="Whole dinars, up to 10,000,000" inputMode="numeric" value={amount} onChange={(e) => handleChange(setAmount, e.currentTarget.value)} placeholder="25000" />
                        <ProblemDetailsAlert problem={problem} />
                        <Button type="submit" disabled={submitting} loading={submitting}>{submitting ? 'Sending…' : 'Send'}</Button>
                    </Stack>
                </form>
            </Card>
            {sent && (
                <Alert role="status" color="teal" variant="light" title={`Sent ${formatMoney(sent.transfer.amount, sent.transfer.currency)} to ${sent.transfer.toUsername}.`}>
                    <Text size="sm">New balance: {formatMoney(sent.transfer.newBalance, sent.transfer.currency)}</Text>
                    {sent.replayed && <Text size="sm" c="dimmed">This transfer was already done, so nothing was sent twice.</Text>}
                    <Anchor component={Link} to="/history" size="sm">View history</Anchor>
                </Alert>
            )}
        </Stack>
    )
}
