import { Anchor, Card } from '@mantine/core'
import { IconArrowsExchange } from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { PageHeader } from '../../shared/components/PageHeader'
import { Pager } from '../../shared/components/Pager'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { TransferTable } from './TransferTable'
import { useTransferHistory } from './useTransferHistory'

const PAGE_SIZE = 10

export function TransactionHistoryPage() {
    const [page, setPage] = useState(1)
    const { data, loading, problem, reload } = useTransferHistory(page, PAGE_SIZE)

    const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

    function goTo(next: number) {
        // Show "Loading…" while the next page is fetched (the effect runs after this render).
        reload()
        setPage(next)
    }

    return (
        <>
            <PageHeader title="Transaction history" description="Everything you sent and received, newest first." />
            <Card>
                {loading && <LoadingState label="Loading transactions…" />}

                {!loading && problem && <ErrorState problem={problem} onRetry={reload} />}

                {!loading && !problem && data && data.totalCount === 0 && (
                    <EmptyState icon={<IconArrowsExchange size={24} stroke={1.75} />} title="Your history is empty">
                        No transactions yet. <Anchor component={Link} to="/send">Send money</Anchor> to get started.
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
