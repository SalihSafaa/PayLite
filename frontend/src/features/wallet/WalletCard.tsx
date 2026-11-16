import { Badge, Button, Group, Text } from '@mantine/core'
import { IconEye, IconEyeOff } from '@tabler/icons-react'
import { useEffect, useRef, useState } from 'react'
import { walletApi } from '../../shared/api/clients'
import type { components } from '../../shared/api/generated/wallet'
import { toProblemDetails } from '../../shared/api/problemDetails'
import { BrandMark } from '../../shared/components/BrandMark'
import { OTP_HEADER, OTP_PROBLEM_TYPE, OtpModal } from '../../shared/components/OtpModal'
import { formatMoney } from '../../shared/lib/money'
import type { WalletDto } from './useMyWallet'

type CardDetails = components['schemas']['CardDetailsDto']

/** How long the full card details stay on screen before they're hidden again. */
export const CARD_DETAILS_VISIBLE_MS = 30_000

/**
 * The balance, shown as a payment card (the Switch app puts its card at the centre of the home screen).
 * The card number is masked; "Show details" asks for the one-time code, then shows the number, expiry and
 * CVV for 30 seconds. The details live only in this component's state: never stored, never logged.
 */
export function WalletCard({ wallet, username }: { wallet: WalletDto; username: string }) {
  const [details, setDetails] = useState<CardDetails | null>(null)
  const [otpOpen, setOtpOpen] = useState(false)
  const [otpError, setOtpError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const hideTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Don't leave a timer running after the card is gone.
  useEffect(() => () => {
    if (hideTimer.current) clearTimeout(hideTimer.current)
  }, [])

  function hide() {
    if (hideTimer.current) clearTimeout(hideTimer.current)
    setDetails(null)
  }

  async function reveal(code: string) {
    setLoading(true)
    setOtpError(null)
    try {
      const { data, error } = await walletApi.GET('/api/wallets/me/card', { params: { header: { [OTP_HEADER]: code } } })
      if (error || !data) {
        const problem = toProblemDetails(error)
        setOtpError(problem.type === OTP_PROBLEM_TYPE ? (problem.title ?? 'The one-time code is wrong.') : (problem.title ?? 'Could not load the card details.'))
        return
      }
      setDetails(data)
      setOtpOpen(false)
      hideTimer.current = setTimeout(() => setDetails(null), CARD_DETAILS_VISIBLE_MS)
    } catch (err) {
      setOtpError(toProblemDetails(err).title ?? 'Could not load the card details.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="wallet-card">
      <div className="wallet-card-mark" aria-hidden="true">
        <BrandMark size={220} />
      </div>
      <Group justify="space-between" align="start">
        <Text c="plum.1" size="sm" fw={500}>IQD wallet</Text>
        {wallet.isMerchant && (
          <Badge variant="white" color="plum" radius="sm">Merchant</Badge>
        )}
      </Group>
      <div>
        <Text c="plum.1" size="sm">Balance</Text>
        <Text className="wallet-card-balance" c="white">{formatMoney(wallet.balance, wallet.currency)}</Text>
      </div>
      <div>
        <Text c="white" className="card-number" aria-label={details ? 'Card number' : 'Card number, hidden'}>
          {details ? details.number : `•••• •••• •••• ${wallet.cardLast4}`}
        </Text>
        {details && (
          <Group gap="lg" mt={4}>
            <Text c="plum.1" size="sm">Expires <Text span c="white" fw={500} className="card-number">{details.expiry}</Text></Text>
            <Text c="plum.1" size="sm">CVV <Text span c="white" fw={500} className="card-number">{details.cvv}</Text></Text>
          </Group>
        )}
      </div>
      <Group justify="space-between" align="end">
        <Text c="white" fw={500}>{username}</Text>
        {details ? (
          <Button size="compact-sm" variant="white" color="plum" leftSection={<IconEyeOff size={14} />} onClick={hide}>
            Hide details
          </Button>
        ) : (
          <Button size="compact-sm" variant="white" color="plum" leftSection={<IconEye size={14} />} onClick={() => { setOtpError(null); setOtpOpen(true) }}>
            Show details
          </Button>
        )}
      </Group>

      <OtpModal
        opened={otpOpen}
        title="Show card details"
        description="Enter the 6-digit code to see the full card number, expiry date and CVV for 30 seconds."
        confirmLabel={loading ? 'Checking…' : 'Confirm'}
        submitting={loading}
        error={otpError}
        onConfirm={(code) => void reveal(code)}
        onClose={() => setOtpOpen(false)}
      />
    </div>
  )
}
