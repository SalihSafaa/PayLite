import { Badge, Group, Stack, Text } from '@mantine/core'
import { IconPlus } from '@tabler/icons-react'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { formatMoney } from '../../shared/lib/money'
import { TOP_UP_STATUS_COLOR, type TopUpRequest } from './topUps'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

/** My top-up requests and their status: Pending until an admin approves (credited) or rejects them. */
export function TopUpRequests({ data, loading, problem, onRetry }: {
  data: TopUpRequest[] | null
  loading: boolean
  problem: Parameters<typeof ErrorState>[0]['problem']
  onRetry: () => void
}) {
  if (loading) return <LoadingState label="Loading top-ups…" />
  if (problem) return <ErrorState problem={problem} onRetry={onRetry} />
  if (!data || data.length === 0) {
    return (
      <EmptyState icon={<IconPlus size={24} stroke={1.75} />} title="No top-ups yet">
        Ask for a top-up and an admin will approve it.
      </EmptyState>
    )
  }

  return (
    <Stack gap="sm">
      {data.slice(0, 5).map((t) => (
        <Group key={t.id} justify="space-between" wrap="nowrap" className="top-up-row">
          <div>
            <Text fw={600} size="sm" className="money">{formatMoney(t.amount, t.currency)}</Text>
            <Text size="xs" c="dimmed">Requested {dateFormat.format(new Date(t.requestedAt))}</Text>
          </div>
          <Badge variant="light" radius="sm" color={TOP_UP_STATUS_COLOR[t.status] ?? 'gray'}>{t.status}</Badge>
        </Group>
      ))}
    </Stack>
  )
}
