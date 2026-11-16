import { Badge, Group, Text } from '@mantine/core'
import { BrandMark } from '../../shared/components/BrandMark'
import { formatMoney } from '../../shared/lib/money'
import type { WalletDto } from './useMyWallet'

/** The balance, shown as a payment card (the Switch app puts its card at the centre of the home screen). */
export function WalletCard({ wallet, username }: { wallet: WalletDto; username: string }) {
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
      <Group justify="space-between" align="end">
        <Text c="white" fw={500}>@{username}</Text>
        <Text c="plum.1" size="xs">PayLite</Text>
      </Group>
    </div>
  )
}
