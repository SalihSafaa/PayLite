import { Anchor, Badge, Button, Card, Divider, Group, Stack, Text, ThemeIcon, Title } from '@mantine/core'
import { IconAlertTriangle, IconCircleCheck, IconCircleX } from '@tabler/icons-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import type { components } from '../../shared/api/generated/wallet'
import type { ProblemDetails } from '../../shared/api/problemDetails'
import { formatMoney } from '../../shared/lib/money'

type TransferResultDto = components['schemas']['TransferResultDto']

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'medium' })

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Group justify="space-between" wrap="nowrap" gap="md">
      <Text c="dimmed" size="sm">{label}</Text>
      <Text size="sm" fw={500} ta="right">{children}</Text>
    </Group>
  )
}

/** The receipt shown after money was sent. */
export function TransferSuccess({ transfer, replayed, onSendAnother }: {
  transfer: TransferResultDto
  replayed: boolean
  onSendAnother: () => void
}) {
  return (
    <Card role="status" maw={520} mx="auto" className="result-card">
      <Stack align="center" gap={6} mb="lg">
        <ThemeIcon size={64} radius="xl" color="teal" variant="light" aria-hidden="true">
          <IconCircleCheck size={40} stroke={1.6} />
        </ThemeIcon>
        <Title order={2} fz={22}>Money sent</Title>
        <Text className="money result-amount" fw={700}>{formatMoney(transfer.amount, transfer.currency)}</Text>
        <Text c="dimmed" size="sm">to <Text span fw={600} c="dark">{transfer.toUsername}</Text></Text>
      </Stack>

      <Stack gap="xs">
        <Row label="Status"><Badge color="teal" variant="light" radius="sm">{transfer.status}</Badge></Row>
        <Row label="Transfer number">#{transfer.transferId}</Row>
        <Row label="Date and time">{dateFormat.format(new Date(transfer.createdAt))}</Row>
        <Divider my={4} />
        <Row label="New balance"><span className="money">{formatMoney(transfer.newBalance, transfer.currency)}</span></Row>
      </Stack>

      {replayed && (
        <Text size="sm" c="dimmed" mt="md" ta="center">
          This transfer was already done, so nothing was sent twice.
        </Text>
      )}

      <Group grow mt="xl">
        <Button variant="default" component={Link} to="/history">View history</Button>
        <Button onClick={onSendAnother}>Send another</Button>
      </Group>
    </Card>
  )
}

/**
 * Shown when the transfer didn't go through. If the server answered (404, 409, 403…) nothing moved.
 * If it didn't (network error, 5xx), we can't know yet, so we say that; retrying is still safe because
 * "Try again" reuses the same Idempotency-Key and the server never sends the same transfer twice.
 */
export function TransferFailure({ problem, attempted, uncertain, onRetry, onEdit }: {
  problem: ProblemDetails
  attempted: { toUsername: string; amount: number }
  uncertain: boolean
  onRetry: () => void
  onEdit: () => void
}) {
  return (
    <Card role="alert" maw={520} mx="auto" className="result-card">
      <Stack align="center" gap={6} mb="lg">
        <ThemeIcon size={64} radius="xl" color={uncertain ? 'switchOrange' : 'red'} variant="light" aria-hidden="true">
          {uncertain ? <IconAlertTriangle size={40} stroke={1.6} /> : <IconCircleX size={40} stroke={1.6} />}
        </ThemeIcon>
        <Title order={2} fz={22}>{uncertain ? 'We couldn\'t confirm the transfer' : 'Transfer failed'}</Title>
        <Text ta="center" fw={500}>{problem.title ?? 'Something went wrong'}</Text>
        {problem.detail && <Text ta="center" size="sm" c="dimmed">{problem.detail}</Text>}
      </Stack>

      <Stack gap="xs">
        <Row label="To">{attempted.toUsername}</Row>
        <Row label="Amount"><span className="money">{formatMoney(attempted.amount)}</span></Row>
        <Row label="Status"><Badge color={uncertain ? 'switchOrange' : 'red'} variant="light" radius="sm">{uncertain ? 'Unknown' : 'Failed'}</Badge></Row>
        {problem.correlationId && <Row label="Reference">{problem.correlationId}</Row>}
      </Stack>

      <Text size="sm" c="dimmed" mt="md" ta="center">
        {uncertain
          ? 'Trying again is safe: the same transfer is never sent twice.'
          : 'No money left your wallet.'}
      </Text>

      <Group grow mt="xl">
        <Button variant="default" onClick={onEdit}>Edit transfer</Button>
        <Button onClick={onRetry}>Try again</Button>
      </Group>
      <Text size="sm" ta="center" mt="sm">
        <Anchor component={Link} to="/history" size="sm">View history</Anchor>
      </Text>
    </Card>
  )
}
