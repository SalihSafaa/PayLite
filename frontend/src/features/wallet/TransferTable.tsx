import { Badge, Group, Table, Text, ThemeIcon } from '@mantine/core'
import { IconArrowDownLeft, IconArrowUpRight } from '@tabler/icons-react'
import { formatMoney } from '../../shared/lib/money'
import type { TransferHistoryItem } from './useTransferHistory'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

/** The full transfer history table (the dashboard uses the lighter ActivityList). */
export function TransferTable({ items }: { items: TransferHistoryItem[] }) {
    return (
        <Table.ScrollContainer minWidth={720}>
            <Table verticalSpacing="md" highlightOnHover className="data-table">
                <Table.Thead>
                    <Table.Tr>
                        <Table.Th>Transaction</Table.Th>
                        <Table.Th>Date</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th ta="right">Amount</Table.Th>
                        <Table.Th ta="right">Balance after</Table.Th>
                    </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                    {items.map((item) => {
                        const sent = item.direction === 'Sent'
                        return (
                            <Table.Tr key={item.transferId}>
                                <Table.Td>
                                    <Group gap="sm" wrap="nowrap">
                                        <ThemeIcon size={36} radius="xl" variant="light" color={sent ? 'plum' : 'teal'} aria-hidden="true">
                                            {sent ? <IconArrowUpRight size={18} stroke={1.75} /> : <IconArrowDownLeft size={18} stroke={1.75} />}
                                        </ThemeIcon>
                                        <div>
                                            <Text fw={600} size="sm">{item.counterpartyUsername}</Text>
                                            <Text size="xs" c="dimmed">{sent ? 'Sent' : 'Received'}</Text>
                                        </div>
                                    </Group>
                                </Table.Td>
                                <Table.Td style={{ whiteSpace: 'nowrap' }}>
                                    <Text size="sm" c="dimmed">{dateFormat.format(new Date(item.createdAt))}</Text>
                                </Table.Td>
                                <Table.Td>
                                    <Badge variant="light" color={item.status === 'Completed' ? 'teal' : 'gray'} radius="sm">
                                        {item.status}
                                    </Badge>
                                </Table.Td>
                                <Table.Td ta="right" style={{ whiteSpace: 'nowrap' }}>
                                    <Text span fw={600} className="money" c={sent ? undefined : 'teal.8'}>
                                        {sent ? '−' : '+'}
                                        {formatMoney(item.amount, item.currency)}
                                    </Text>
                                </Table.Td>
                                <Table.Td ta="right" style={{ whiteSpace: 'nowrap' }}>
                                    <Text span size="sm" c="dimmed" className="money">
                                        {item.balanceAfter != null ? formatMoney(item.balanceAfter, item.currency) : '—'}
                                    </Text>
                                </Table.Td>
                            </Table.Tr>
                        )
                    })}
                </Table.Tbody>
            </Table>
        </Table.ScrollContainer>
    )
}
