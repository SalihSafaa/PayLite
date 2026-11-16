import { Button, Card, Chip, Divider, Grid, Group, Stack, Text, TextInput } from '@mantine/core'
import { useRef, useState, type FormEvent } from 'react'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { OTP_HEADER, OTP_PROBLEM_TYPE, OtpModal } from '../../shared/components/OtpModal'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { TransferFailure, TransferSuccess } from './TransferResult'
import { PageHeader } from '../../shared/components/PageHeader'
import { formatMoney } from '../../shared/lib/money'
import { useMyWallet } from './useMyWallet'

type TransferResultDto = components['schemas']['TransferResultDto']

const MAX_TRANSFER = 10_000_000 // same as Wallet:MaxTransferAmount on the server, which has the final say
const IDEMPOTENCY_HEADER = 'Idempotency-Key' // keep in sync with Shared.Contracts/Http/CorrelationHeaders.cs
const QUICK_AMOUNTS = [5_000, 10_000, 25_000, 50_000]

export function SendMoneyPage() {
    const { wallet, setWallet } = useMyWallet()
    const [toUsername, setToUsername] = useState('')
    const [amount, setAmount] = useState('')
    const [submitting, setSubmitting] = useState(false)
    const [problem, setProblem] = useState<ProblemDetails | null>(null)
    // After Confirm: the receipt, or what went wrong. Either one replaces the form.
    const [result, setResult] = useState<
        | { kind: 'success'; transfer: TransferResultDto; replayed: boolean }
        | { kind: 'failure'; problem: ProblemDetails; attempted: { toUsername: string; amount: number }; uncertain: boolean }
        | null
    >(null)
    // One key per filled-in form, reused on every retry so the server never moves the money twice (ADR-0002).
    const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
    // A ref updates at once, so a fast double-click can't send before `submitting` re-renders the button.
    const inFlight = useRef(false)
    // Sending money needs the one-time code: Send opens the dialog, the dialog's Confirm sends.
    const [otpOpen, setOtpOpen] = useState(false)
    const [otpError, setOtpError] = useState<string | null>(null)
    // The validated form, waiting for the code.
    const [pending, setPending] = useState<{ toUsername: string; amount: number } | null>(null)

    // Editing the form makes it a new transfer, so it gets a new key.
    function handleChange(setter: (value: string) => void, value: string) {
        setter(value)
        setIdempotencyKey(crypto.randomUUID())
    }

    // Step 1: check the form, then ask for the code. Nothing is sent yet.
    function handleSubmit(event: FormEvent) {
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

        setProblem(null)
        setOtpError(null)
        setPending({ toUsername: username, amount: value })
        setOtpOpen(true)
    }

    // Step 2: send with the code. The Idempotency-Key is the form's key, the same on every retry,
    // including a retry after a wrong code (the server stores nothing for a refused code).
    async function confirmTransfer(code: string) {
        if (inFlight.current || !pending) return

        inFlight.current = true
        setSubmitting(true)
        setOtpError(null)
        try {
            const { data, error, response } = await walletApi.POST('/api/transfers', {
                params: { header: { [IDEMPOTENCY_HEADER]: idempotencyKey, [OTP_HEADER]: code } },
                body: pending,
            })
            if (error || !data) {
                const problemDetails = toProblemDetails(error)
                if (problemDetails.type === OTP_PROBLEM_TYPE) {
                    // Wrong code: keep the dialog open so the user can type it again.
                    setOtpError(problemDetails.title ?? 'The one-time code is wrong.')
                    return
                }
                // The server answered with a refusal (404, 409, 403…): nothing moved.
                // The key is kept, so "Try again" is a real retry of the same transfer.
                setOtpOpen(false)
                setResult({ kind: 'failure', problem: problemDetails, attempted: pending, uncertain: response.status >= 500 })
                return
            }
            setOtpOpen(false)
            setPending(null)
            setResult({ kind: 'success', transfer: data, replayed: response.headers.get('Idempotent-Replayed') === 'true' })
            if (wallet) setWallet({ ...wallet, balance: data.newBalance })
            setToUsername('')
            setAmount('')
            setIdempotencyKey(crypto.randomUUID())
        } catch (err) {
            // Network error: we don't know if the server got it. Retrying with the same key is safe.
            setOtpOpen(false)
            setResult({ kind: 'failure', problem: toProblemDetails(err), attempted: pending, uncertain: true })
        } finally {
            inFlight.current = false
            setSubmitting(false)
        }
    }

    // Live preview, only a hint: the server checks the balance and has the final say.
    const previewAmount = /^\d+$/.test(amount.trim()) ? Number(amount) : null
    const balanceAfter = wallet && previewAmount !== null ? wallet.balance - previewAmount : null

    const otpDialog = (
        <OtpModal
            opened={otpOpen}
            title="Confirm with your code"
            description={pending ? `Enter the 6-digit code to send ${formatMoney(pending.amount)} to ${pending.toUsername}.` : ''}
            confirmLabel={submitting ? 'Sending…' : 'Confirm and send'}
            submitting={submitting}
            error={otpError}
            onConfirm={(code) => void confirmTransfer(code)}
            onClose={() => setOtpOpen(false)}
        />
    )

    if (result?.kind === 'success') {
        return (
            <>
                <PageHeader title="Send money" />
                <TransferSuccess transfer={result.transfer} replayed={result.replayed} onSendAnother={() => setResult(null)} />
            </>
        )
    }

    if (result?.kind === 'failure') {
        return (
            <>
                <PageHeader title="Send money" />
                <TransferFailure
                    problem={result.problem}
                    attempted={result.attempted}
                    uncertain={result.uncertain}
                    // Same transfer, same key: the code again, then the same request. It can't be sent twice.
                    onRetry={() => {
                        setOtpError(null)
                        setOtpOpen(true)
                    }}
                    // Back to the form with the values kept; changing them makes it a new transfer (new key).
                    onEdit={() => setResult(null)}
                />
                {otpDialog}
            </>
        )
    }

    return (
        <>
            <PageHeader title="Send money" description="Send IQD to anyone with a PayLite username. It arrives straight away." />
            <Grid gap="lg">
                <Grid.Col span={{ base: 12, md: 7 }}>
                <Card>
                    <form onSubmit={handleSubmit} noValidate>
                        <Stack gap="lg">
                            <TextInput label="Recipient username" value={toUsername} onChange={(e) => handleChange(setToUsername, e.currentTarget.value)} autoComplete="off" placeholder="e.g. coffeeshop" />
                            <div>
                                <TextInput
                                    label="Amount (IQD)"
                                    description="Whole dinars, up to 10,000,000"
                                    inputMode="numeric"
                                    value={amount}
                                    onChange={(e) => handleChange(setAmount, e.currentTarget.value)}
                                    placeholder="25000"
                                    size="lg"
                                    classNames={{ input: 'amount-input' }}
                                    rightSection={<Text c="dimmed" fw={500} size="sm">IQD</Text>}
                                    rightSectionWidth={56}
                                />
                                <Group gap="xs" mt="sm">
                                    {QUICK_AMOUNTS.map((q) => (
                                        <Chip key={q} checked={amount === String(q)} onChange={() => handleChange(setAmount, String(q))} variant="outline" color="plum" size="sm">
                                            {formatMoney(q)}
                                        </Chip>
                                    ))}
                                </Group>
                            </div>
                            <ProblemDetailsAlert problem={problem} />
                            <Button type="submit" size="md" disabled={submitting}>Send</Button>
                        </Stack>
                    </form>
                </Card>
                </Grid.Col>

                <Grid.Col span={{ base: 12, md: 5 }}>
                <Stack gap="lg">
                    <Card className="preview-card">
                        <Text fw={600} mb="sm">Summary</Text>
                        <Group justify="space-between">
                            <Text c="dimmed" size="sm">To</Text>
                            <Text fw={500} size="sm">{toUsername.trim() || '—'}</Text>
                        </Group>
                        <Group justify="space-between" mt={6}>
                            <Text c="dimmed" size="sm">You send</Text>
                            <Text fw={600} className="money">{previewAmount !== null ? formatMoney(previewAmount) : '—'}</Text>
                        </Group>
                        <Divider my="sm" />
                        <Group justify="space-between">
                            <Text c="dimmed" size="sm">Your balance</Text>
                            <Text size="sm" className="money">{wallet ? formatMoney(wallet.balance, wallet.currency) : '—'}</Text>
                        </Group>
                        <Group justify="space-between" mt={6}>
                            <Text c="dimmed" size="sm">Balance after</Text>
                            <Text size="sm" fw={600} className="money" c={balanceAfter !== null && balanceAfter < 0 ? 'red.8' : undefined}>
                                {balanceAfter !== null ? formatMoney(balanceAfter) : '—'}
                            </Text>
                        </Group>
                        {balanceAfter !== null && balanceAfter < 0 && (
                            <Text size="xs" c="red.8" mt="xs">That is more than your balance.</Text>
                        )}
                    </Card>
                </Stack>
                </Grid.Col>
            </Grid>

            {otpDialog}
        </>
    )
}
