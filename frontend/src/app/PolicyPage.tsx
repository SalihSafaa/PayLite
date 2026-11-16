import { Anchor, Card, List, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router'
import { useAuth } from '../shared/auth/useAuth'
import { PageHeader } from '../shared/components/PageHeader'
import { formatMoney } from '../shared/lib/money'

// Keep these in sync with Wallet:MaxTransferAmount / Wallet:MaxTopUpAmount in the Wallet API's
// appsettings.json. The server enforces them; this page only explains them.
const MAX_TRANSFER = 10_000_000
const MAX_TOP_UP = 1_000_000

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <Title order={2} fz="lg" mb="sm">{title}</Title>
      {children}
    </Card>
  )
}

/** The wallet's rules in plain words. Public: readable before you register. */
export function PolicyPage() {
  const { user } = useAuth()

  return (
    <div className={user ? undefined : 'page'}>
      <PageHeader title="Wallet policy" description="The rules every PayLite wallet follows." />
      <Stack gap="lg">
        <Section title="Money">
          <List spacing="xs">
            <List.Item>Every wallet holds Iraqi dinars (IQD), in whole dinars only. There are no fils.</List.Item>
            <List.Item>A balance can never go below zero. A transfer that would need more than you have is refused, and nothing moves.</List.Item>
            <List.Item>Sending money and receiving it is free. PayLite charges no fees.</List.Item>
          </List>
        </Section>

        <Section title="Limits">
          <List spacing="xs">
            <List.Item>One transfer: up to {formatMoney(MAX_TRANSFER)}.</List.Item>
            <List.Item>One top-up: up to {formatMoney(MAX_TOP_UP)}.</List.Item>
          </List>
        </Section>

        <Section title="Transfers">
          <List spacing="xs">
            <List.Item>A transfer completes at once: the money leaves your wallet and arrives in the other wallet in the same step. There is no "pending" transfer.</List.Item>
            <List.Item>Pressing Send twice (or retrying after a lost connection) never sends the money twice.</List.Item>
            <List.Item>Transfers can't be cancelled once they are completed. Check the username before you send.</List.Item>
          </List>
        </Section>

        <Section title="Your account">
          <List spacing="xs">
            <List.Item>You sign in with your username and password. PayLite never asks for your password anywhere else.</List.Item>
            <List.Item>Your session stays signed in on this browser until you log out, for up to 7 days.</List.Item>
            <List.Item>Personal accounts send and receive money. Merchant accounts receive payments from customers.</List.Item>
          </List>
        </Section>

        <Text size="sm" c="dimmed">
          PayLite is a training project; no real money is involved.{' '}
          {user ? <Anchor component={Link} to="/">Back to your wallet</Anchor> : <Anchor component={Link} to="/login">Log in</Anchor>}
        </Text>
      </Stack>
    </div>
  )
}
