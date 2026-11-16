import { Group, Stack, Text, ThemeIcon, UnstyledButton } from '@mantine/core'
import { IconArrowDownLeft, IconArrowUpRight } from '@tabler/icons-react'
import { Link } from 'react-router'
import { formatMoney } from '../../shared/lib/money'
import type { TransferHistoryItem } from './useTransferHistory'

const dayFormat = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' })
const timeFormat = new Intl.DateTimeFormat('en-GB', { timeStyle: 'short' })

function when(iso: string): string {
  const date = new Date(iso)
  const today = new Date()
  const yesterday = new Date(today)
  yesterday.setDate(today.getDate() - 1)
  const same = (a: Date, b: Date) => a.toDateString() === b.toDateString()
  const day = same(date, today) ? 'Today' : same(date, yesterday) ? 'Yesterday' : dayFormat.format(date)
  return `${day}, ${timeFormat.format(date)}`
}

/** The dashboard's recent activity: who, when and how much, newest first. */
export function ActivityList({ items }: { items: TransferHistoryItem[] }) {
  return (
    <Stack gap={0} component="ul" className="activity-list">
      {items.map((item) => {
        const sent = item.direction === 'Sent'
        return (
          <li key={item.transferId}>
            <UnstyledButton component={Link} to="/history" className="activity-row">
              <Group justify="space-between" wrap="nowrap" gap="md">
                <Group gap="sm" wrap="nowrap" miw={0}>
                  <ThemeIcon size={40} radius="xl" variant="light" color={sent ? 'plum' : 'teal'} aria-hidden="true">
                    {sent ? <IconArrowUpRight size={20} stroke={1.75} /> : <IconArrowDownLeft size={20} stroke={1.75} />}
                  </ThemeIcon>
                  <div style={{ minWidth: 0 }}>
                    <Text fw={600} truncate>{item.counterpartyUsername}</Text>
                    <Text size="sm" c="dimmed">{sent ? 'Sent' : 'Received'} · {when(item.createdAt)}</Text>
                  </div>
                </Group>
                <Text fw={600} className="money" c={sent ? undefined : 'teal.8'} style={{ whiteSpace: 'nowrap' }}>
                  {sent ? '−' : '+'}{formatMoney(item.amount, item.currency)}
                </Text>
              </Group>
            </UnstyledButton>
          </li>
        )
      })}
    </Stack>
  )
}
