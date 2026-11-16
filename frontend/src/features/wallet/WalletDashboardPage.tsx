import { Anchor, Badge, Button, Card, Group, Loader, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import { TopUpForm } from './TopUpForm'
import { TransferTable } from './TransferTable'
import { useMyWallet } from './useMyWallet'
import { useTransferHistory } from './useTransferHistory'

function RecentTransactions() {
    const { data, loading, problem, reload } = useTransferHistory(1, 5)

    if (loading) {
        return (
            <Group gap="xs">
                <Loader size="xs" />
                <Text c="dimmed" size="sm">Loading transactions…</Text>
            </Group>
        )
    }
    if (problem) {
        return (
            <Stack align="start">
                <ProblemDetailsAlert problem={problem} />
                <Button variant="default" onClick={reload}>Try again</Button>
            </Stack>
        )
    }
    if (!data || data.totalCount === 0) {
        return (
            <Text c="dimmed">
                No transactions yet. <Anchor component={Link} to="/send">Send money</Anchor>
            </Text>
        )
    }
    return (
        <>
            <TransferTable items={data.items} />
            {data.totalCount > data.items.length && (
                <Anchor component={Link} to="/history" size="sm">See all {data.totalCount} transactions</Anchor>
            )}
        </>
    )
}

export function WalletDashboardPage() {
    const { wallet, setWallet, loading, problem, reload } = useMyWallet()

    if (loading && !wallet) {
        return (
            <Group gap="xs">
                <Loader size="sm" />
                <Text c="dimmed">Loading your wallet…</Text>
            </Group>
        )
    }

    if (problem && !wallet) {
        return (
            <Stack align="start">
                <ProblemDetailsAlert problem={problem} />
                <Button variant="default" onClick={reload}>Try again</Button>
            </Stack>
        )
    }

    if (!wallet) return null

    return (
        <Stack>
            <Title order={2}>Wallet</Title>
            <Card withBorder radius="md" p="lg">
                <Group justify="space-between" align="start">
                    <div>
                        <Text c="dimmed" size="sm">Balance</Text>
                        <Text fz={36} fw={700} lh={1.2}>{formatMoney(wallet.balance, wallet.currency)}</Text>
                    </div>
                    {wallet.isMerchant && <Badge size="lg" variant="light">Merchant</Badge>}
                </Group>
                <Group mt="md" align="end" justify="space-between">
                    <TopUpForm onToppedUp={setWallet} />
                    <Button component={Link} to="/send">Send money</Button>
                </Group>
            </Card>
            <Card withBorder radius="md" p="lg">
                <Title order={4} mb="sm">Recent transactions</Title>
                <RecentTransactions />
            </Card>
        </Stack>
    )
}
