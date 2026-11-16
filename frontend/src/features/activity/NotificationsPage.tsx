import { Button, Card, Checkbox, Group, Text, ThemeIcon } from '@mantine/core'
import { IconArrowDownLeft, IconBellOff } from '@tabler/icons-react'
import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { formatMoney } from '../../shared/lib/money'
import { useNotifications } from './useNotifications'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

export function NotificationsPage() {
  const [unreadOnly, setUnreadOnly] = useState(false)
  const { items, loading, problem, reload, markAsRead, markingId, markProblem } = useNotifications(unreadOnly)

  function changeFilter(next: boolean) {
    // Show "Loading…" while the other list is fetched (the effect runs after this render).
    reload()
    setUnreadOnly(next)
  }

  return (
    <>
      <PageHeader
        title="Notifications"
        description="Money you receive, live."
        action={<Checkbox label="Unread only" checked={unreadOnly} onChange={(e) => changeFilter(e.currentTarget.checked)} color="plum" />}
      />
      <Card>
        {loading && <LoadingState label="Loading notifications…" />}

        {!loading && problem && <ErrorState problem={problem} onRetry={reload} />}

        {!loading && !problem && items.length === 0 && (
          <EmptyState icon={<IconBellOff size={24} stroke={1.75} />} title={unreadOnly ? 'All caught up' : 'Nothing here yet'}>
            {unreadOnly ? 'No unread notifications.' : 'No notifications yet. You will see money you receive here, live.'}
          </EmptyState>
        )}

        {!loading && !problem && items.length > 0 && (
          <>
            <ProblemDetailsAlert problem={markProblem} />
            <ul className="notification-list">
              {items.map((n) => (
                <li key={n.id} className={n.readAt ? 'read' : 'unread'}>
                  <Group gap="sm" wrap="nowrap" miw={0}>
                    <ThemeIcon size={40} radius="xl" variant="light" color="teal" aria-hidden="true">
                      <IconArrowDownLeft size={20} stroke={1.75} />
                    </ThemeIcon>
                    <div style={{ minWidth: 0 }}>
                      <Text size="sm">
                        <strong className="money">{formatMoney(n.amount, n.currency)}</strong> received from <strong>{n.fromUsername}</strong>
                      </Text>
                      <Text size="xs" c="dimmed">{dateFormat.format(new Date(n.occurredAt))}</Text>
                    </div>
                  </Group>
                  {n.readAt ? (
                    <Text size="sm" c="dimmed">Read</Text>
                  ) : (
                    <Button size="xs" variant="light" color="plum" onClick={() => void markAsRead(n.id)} disabled={markingId === n.id}>
                      {markingId === n.id ? 'Marking…' : 'Mark as read'}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </>
        )}
      </Card>
    </>
  )
}
