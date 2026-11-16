import { Anchor, Button, Card, Group, Loader, Stack, Text, Title } from '@mantine/core'
import { useState } from 'react'
import { Link } from 'react-router'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
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
        <Stack>
            <Title order={2}>Transaction history</Title>
            <Card withBorder radius="md" p="lg">
                {loading && (
                    <Group gap="xs">
                        <Loader size="xs" />
                        <Text c="dimmed" size="sm">Loading transactions…</Text>
                    </Group>
                )}

                {!loading && problem && (
                    <Stack align="start">
                        <ProblemDetailsAlert problem={problem} />
                        <Button variant="default" onClick={reload}>Try again</Button>
                    </Stack>
                )}

                {!loading && !problem && data && data.totalCount === 0 && (
                    <Text c="dimmed">
                        No transactions yet. <Anchor component={Link} to="/send">Send money</Anchor> to get started.
                    </Text>
                )}

                {!loading && !problem && data && data.totalCount > 0 && (
                    <Stack>
                        <TransferTable items={data.items} />
                        <Group justify="space-between">
                            <Button variant="default" onClick={() => goTo(page - 1)} disabled={page <= 1}>
                                Previous
                            </Button>
                            <Text size="sm" c="dimmed">
                                Page {page} of {totalPages} · {data.totalCount} {data.totalCount === 1 ? 'transaction' : 'transactions'}
                            </Text>
                            <Button variant="default" onClick={() => goTo(page + 1)} disabled={page >= totalPages}>
                                Next
                            </Button>
                        </Group>
                    </Stack>
                )}
            </Card>
        </Stack>
    )
}
