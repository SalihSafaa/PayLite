import { Anchor, Card, SegmentedControl } from '@mantine/core'
import { IconArrowsExchange } from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { useAuth } from '../../shared/auth/useAuth'
import { PageHeader } from '../../shared/components/PageHeader'
import { Pager } from '../../shared/components/Pager'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { TransferTable } from './TransferTable'
import { useTransferHistory, type TransferDirectionFilter } from './useTransferHistory'

const PAGE_SIZE = 10

export function TransactionHistoryPage() {
    const { user } = useAuth()
    const isMerchant = user?.role === 'Merchant'
    const [page, setPage] = useState(1)
    const [direction, setDirection] = useState<TransferDirectionFilter>('All')
    const { data, loading, problem, reload } = useTransferHistory(page, PAGE_SIZE, direction)

    const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

    function goTo(next: number) {
        // Show "Loading…" while the next page is fetched (the effect runs after this render).
        reload()
        setPage(next)
    }

    function changeDirection(next: string) {
        reload()
        setDirection(next as TransferDirectionFilter)
        setPage(1)
    }

    const emptyText = direction === 'Sent' ? 'You haven\'t sent any money yet.'
        : direction === 'Received' ? 'You haven\'t received any money yet.'
        : null

    return (
        <>
            <PageHeader title="Transaction history" description="Everything you sent and received, newest first." />
            <Card>
                <SegmentedControl
                    mb="md"
                    value={direction}
                    onChange={changeDirection}
                    aria-label="Show"
                    data={[
                        { label: 'All', value: 'All' },
                        { label: 'Sent', value: 'Sent' },
                        { label: 'Received', value: 'Received' },
                    ]}
                />

                {loading && <LoadingState label="Loading transactions…" />}

                {!loading && problem && <ErrorState problem={problem} onRetry={reload} />}

                {!loading && !problem && data && data.totalCount === 0 && emptyText && (
                    <EmptyState icon={<IconArrowsExchange size={24} stroke={1.75} />} title="Nothing here">
                        {emptyText}
                    </EmptyState>
                )}

                {!loading && !problem && data && data.totalCount === 0 && !emptyText && (
                    <EmptyState icon={<IconArrowsExchange size={24} stroke={1.75} />} title="Your history is empty">
                        {isMerchant ? (
                            'No transactions yet. Payments from your customers show up here.'
                        ) : (
                            <>No transactions yet. <Anchor component={Link} to="/send">Send money</Anchor> to get started.</>
                        )}
                    </EmptyState>
                )}

                {!loading && !problem && data && data.totalCount > 0 && (
                    <>
                        <TransferTable items={data.items} />
                        <Pager page={page} totalPages={totalPages} total={data.totalCount} noun={['transaction', 'transactions']} onPage={goTo} />
                    </>
                )}
            </Card>
        </>
    )
}
