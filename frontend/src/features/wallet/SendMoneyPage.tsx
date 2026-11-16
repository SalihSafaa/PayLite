import { Alert, Anchor, Button, Card, Chip, Divider, Grid, Group, Stack, Text, TextInput } from '@mantine/core'
import { IconCircleCheck } from '@tabler/icons-react'
import { useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
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
            if (wallet) setWallet({ ...wallet, balance: data.newBalance })
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

    // Live preview, only a hint: the server checks the balance and has the final say.
    const previewAmount = /^\d+$/.test(amount.trim()) ? Number(amount) : null
    const balanceAfter = wallet && previewAmount !== null ? wallet.balance - previewAmount : null

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
                            <Button type="submit" size="md" disabled={submitting} loading={submitting}>{submitting ? 'Sending…' : 'Send'}</Button>
                        </Stack>
                    </form>
                </Card>
                </Grid.Col>

                <Grid.Col span={{ base: 12, md: 5 }}>
                <Stack gap="lg">
                    {sent && (
                        <Alert role="status" color="teal" variant="light" radius="lg" icon={<IconCircleCheck size={22} stroke={1.75} />}
                            title={`Sent ${formatMoney(sent.transfer.amount, sent.transfer.currency)} to ${sent.transfer.toUsername}.`}>
                            <Text size="sm">New balance: {formatMoney(sent.transfer.newBalance, sent.transfer.currency)}</Text>
                            {sent.replayed && <Text size="sm" c="dimmed">This transfer was already done, so nothing was sent twice.</Text>}
                            <Anchor component={Link} to="/history" size="sm" fw={500}>View history</Anchor>
                        </Alert>
                    )}
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
        </>
    )
}
