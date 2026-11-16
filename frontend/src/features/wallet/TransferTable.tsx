import { Badge, Table, Text } from '@mantine/core'
import { formatMoney } from '../../shared/lib/money'
import type { TransferHistoryItem } from './useTransferHistory'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

/** Shared by the dashboard (last 5) and the history page. */
export function TransferTable({ items }: { items: TransferHistoryItem[] }) {
    return (
        <Table.ScrollContainer minWidth={620}>
            <Table verticalSpacing="sm" highlightOnHover>
                <Table.Thead>
                    <Table.Tr>
                        <Table.Th>Date</Table.Th>
                        <Table.Th>Type</Table.Th>
                        <Table.Th>With</Table.Th>
                        <Table.Th ta="right">Amount</Table.Th>
                        <Table.Th ta="right">Balance after</Table.Th>
                    </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                    {items.map((item) => {
                        const sent = item.direction === 'Sent'
                        return (
                            <Table.Tr key={item.transferId}>
                                <Table.Td style={{ whiteSpace: 'nowrap' }}>{dateFormat.format(new Date(item.createdAt))}</Table.Td>
                                <Table.Td style={{ whiteSpace: 'nowrap' }}>
                                    <Badge variant="light" color={sent ? 'red' : 'teal'} style={{ flexShrink: 0, overflow: 'visible' }}>{sent ? 'Sent' : 'Received'}</Badge>
                                </Table.Td>
                                <Table.Td>{item.counterpartyUsername}</Table.Td>
                                <Table.Td ta="right">
                                    <Text span fw={600} c={sent ? 'red.7' : 'teal.7'}>
                                        {sent ? '−' : '+'}
                                        {formatMoney(item.amount, item.currency)}
                                    </Text>
                                </Table.Td>
                                <Table.Td ta="right" c="dimmed">
                                    {item.balanceAfter != null ? formatMoney(item.balanceAfter, item.currency) : '—'}
                                </Table.Td>
                            </Table.Tr>
                        )
                    })}
                </Table.Tbody>
            </Table>
        </Table.ScrollContainer>
    )
}
