import { PlaceholderPage } from '../../shared/components/PlaceholderPage'

export function SendMoneyPage() {
  return (
    <PlaceholderPage
      title="Send money"
      owner="A"
      todo="POST /api/transfers with an Idempotency-Key (crypto.randomUUID), reused on retry; button disabled while in flight"
    />
  )
}
