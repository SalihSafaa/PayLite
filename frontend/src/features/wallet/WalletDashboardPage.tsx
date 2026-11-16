import { Anchor, Card, Group, Modal, SimpleGrid, Text, Title, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconArrowsExchange, IconBell, IconChartBar, IconHistory, IconPlus, IconReceipt, IconSend } from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useAuth } from '../../shared/auth/useAuth'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { ActivityList } from './ActivityList'
import { TopUpForm } from './TopUpForm'
import { useMyWallet } from './useMyWallet'
import { useTransferHistory } from './useTransferHistory'
import { WalletCard } from './WalletCard'

function QuickAction({ icon, label, to, onClick }: { icon: ReactNode; label: string; to?: string; onClick?: () => void }) {
    const content = (
        <>
            <span className="quick-action-icon" aria-hidden="true">{icon}</span>
            <Text fw={600} size="sm">{label}</Text>
        </>
    )
    return to ? (
        <UnstyledButton component={Link} to={to} className="quick-action">{content}</UnstyledButton>
    ) : (
        <UnstyledButton onClick={onClick} className="quick-action">{content}</UnstyledButton>
    )
}

function RecentTransactions({ isMerchant }: { isMerchant: boolean }) {
    const { data, loading, problem, reload } = useTransferHistory(1, 5)

    if (loading) return <LoadingState label="Loading transactions…" />
    if (problem) return <ErrorState problem={problem} onRetry={reload} />
    if (!data || data.totalCount === 0) {
        return (
            <EmptyState icon={<IconArrowsExchange size={24} stroke={1.75} />} title="No transactions yet">
                {isMerchant ? (
                    'Payments from your customers show up here.'
                ) : (
                    <>Money you send and receive shows up here. <Anchor component={Link} to="/send">Send money</Anchor></>
                )}
            </EmptyState>
        )
    }
    return <ActivityList items={data.items} />
}

export function WalletDashboardPage() {
    const { user } = useAuth()
    const { wallet, setWallet, loading, problem, reload } = useMyWallet()
    const [topUpOpen, topUp] = useDisclosure(false)

    if (loading && !wallet) return <LoadingState label="Loading your wallet…" />
    if (problem && !wallet) return <ErrorState problem={problem} onRetry={reload} />
    if (!wallet) return null

    return (
        <>
            <PageHeader title={`Hello, ${user?.username ?? ''}`} description="Here is your wallet." />

            <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                <WalletCard wallet={wallet} username={user?.username ?? ''} />
                <SimpleGrid cols={2} spacing="md">
                    {wallet.isMerchant ? (
                        <QuickAction icon={<IconChartBar size={22} stroke={1.75} />} label="Daily summary" to="/merchant/summary" />
                    ) : (
                        <QuickAction icon={<IconSend size={22} stroke={1.75} />} label="Send money" to="/send" />
                    )}
                    <QuickAction icon={<IconPlus size={22} stroke={1.75} />} label="Top up" onClick={topUp.open} />
                    <QuickAction icon={<IconHistory size={22} stroke={1.75} />} label="History" to="/history" />
                    {wallet.isMerchant ? (
                        <QuickAction icon={<IconReceipt size={22} stroke={1.75} />} label="Payments" to="/merchant/payments" />
                    ) : (
                        <QuickAction icon={<IconBell size={22} stroke={1.75} />} label="Notifications" to="/notifications" />
                    )}
                </SimpleGrid>
            </SimpleGrid>

            <Card mt="lg">
                <Group justify="space-between" mb="xs">
                    <Title order={2} fz="lg">Recent activity</Title>
                    <Anchor component={Link} to="/history" size="sm" fw={500}>See all</Anchor>
                </Group>
                <RecentTransactions isMerchant={wallet.isMerchant} />
            </Card>

            <Modal opened={topUpOpen} onClose={topUp.close} title="Top up your wallet" centered radius="lg">
                <TopUpForm
                    onToppedUp={(updated) => {
                        setWallet(updated)
                        topUp.close()
                    }}
                />
            </Modal>
        </>
    )
}
