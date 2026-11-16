import { Button, Chip, Group, Stack, TextInput } from '@mantine/core'
import { useState, type FormEvent } from 'react'
import { walletApi } from '../../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import type { WalletDto } from './useMyWallet'

const MAX_TOP_UP = 1_000_000 // same as Wallet:MaxTopUpAmount on the server, which has the final say
const QUICK_AMOUNTS = [10_000, 25_000, 50_000, 100_000]

export function TopUpForm({ onToppedUp }: { onToppedUp: (wallet: WalletDto) => void }) {
    const [amount, setAmount] = useState('')
    const [submitting, setSubmitting] = useState(false)
    const [problem, setProblem] = useState<ProblemDetails | null>(null)

    async function handleSubmit(event: FormEvent) {
        event.preventDefault()
        const value = Number(amount)
        if (!/^\d+$/.test(amount.trim()) || value <= 0) {
            setProblem({ title: 'Enter a whole number of dinars greater than zero.' })
            return
        }
        if (value > MAX_TOP_UP) {
            setProblem({ title: `A single top-up cannot exceed ${formatMoney(MAX_TOP_UP)}.` })
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
            <Stack>
                <TextInput
                    label="Amount (IQD)"
                    description={`Up to ${formatMoney(MAX_TOP_UP)} at a time`}
                    inputMode="numeric"
                    value={amount}
                    onChange={(e) => setAmount(e.currentTarget.value)}
                    placeholder="50000"
                    data-autofocus
                />
                <Group gap="xs">
                    {QUICK_AMOUNTS.map((q) => (
                        <Chip key={q} checked={amount === String(q)} onChange={() => setAmount(String(q))} variant="outline" color="plum" size="sm">
                            {formatMoney(q)}
                        </Chip>
                    ))}
                </Group>
                <ProblemDetailsAlert problem={problem} />
                <Button type="submit" loading={submitting} fullWidth>Top up</Button>
            </Stack>
        </form>
    )
}
