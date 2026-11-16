import { MantineProvider } from '@mantine/core'
import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { theme } from '../../shared/theme'
import { CARD_DETAILS_VISIBLE_MS, WalletCard } from './WalletCard'
import type { WalletDto } from './useMyWallet'

vi.mock('../../shared/api/clients', () => ({ walletApi: { GET: vi.fn() } }))
const get = vi.mocked(walletApi.GET)
type GetResult = Awaited<ReturnType<typeof walletApi.GET>>

const wallet: WalletDto = { id: 7, balance: 25_000, currency: 'IQD', isMerchant: false, cardLast4: '4821' }
const card = { holderName: 'ali', number: '5000 0000 5544 4821', expiry: '11/30', cvv: '359' }

function renderCard() {
  render(
    <MantineProvider theme={theme}>
      <WalletCard wallet={wallet} username="ali" />
    </MantineProvider>,
  )
  return userEvent.setup()
}

async function reveal(user: ReturnType<typeof userEvent.setup>, code = '123456') {
  await user.click(screen.getByRole('button', { name: 'Show details' }))
  await user.type(await screen.findByLabelText('Digit 1 of 6'), code)
  await user.click(screen.getByRole('button', { name: 'Confirm' }))
}

describe('WalletCard', () => {
  beforeEach(() => get.mockReset())
  afterEach(() => {
    cleanup()
    vi.useRealTimers()
  })

  it('shows the name without "@" and only the last four digits', () => {
    renderCard()

    expect(screen.getByText('ali')).toBeTruthy()
    expect(screen.queryByText('@ali')).toBeNull()
    expect(screen.getByLabelText('Card number, hidden').textContent).toBe('•••• •••• •••• 4821')
    expect(screen.queryByText(/CVV/)).toBeNull()
  })

  it('shows the full details after the right code, with the code in the request', async () => {
    get.mockResolvedValueOnce({ data: card, error: undefined, response: new Response() } as GetResult)
    const user = renderCard()

    await reveal(user)

    expect((await screen.findByLabelText('Card number')).textContent).toBe('5000 0000 5544 4821')
    expect(screen.getByText('11/30')).toBeTruthy()
    expect(screen.getByText('359')).toBeTruthy()
    expect(get).toHaveBeenCalledWith('/api/wallets/me/card', { params: { header: { 'X-OTP-Code': '123456' } } })
  })

  it('keeps the dialog open and shows nothing on a wrong code', async () => {
    get.mockResolvedValueOnce({
      data: undefined,
      error: { status: 403, type: 'urn:paylite:otp-invalid', title: 'The one-time code is wrong.' },
      response: new Response(null, { status: 403 }),
    } as GetResult)
    const user = renderCard()

    await reveal(user, '000000')

    expect((await screen.findByRole('alert')).textContent).toContain('The one-time code is wrong.')
    expect(screen.queryByText('359')).toBeNull()
  })

  it('hides the details when "Hide details" is pressed', async () => {
    get.mockResolvedValueOnce({ data: card, error: undefined, response: new Response() } as GetResult)
    const user = renderCard()
    await reveal(user)
    await screen.findByText('359')

    await user.click(screen.getByRole('button', { name: 'Hide details' }))

    expect(screen.queryByText('359')).toBeNull()
    expect(screen.getByLabelText('Card number, hidden')).toBeTruthy()
  })

  it('hides the details on its own when the 30 seconds are up', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    get.mockResolvedValueOnce({ data: card, error: undefined, response: new Response() } as GetResult)
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(
      <MantineProvider theme={theme}>
        <WalletCard wallet={wallet} username="ali" />
      </MantineProvider>,
    )

    await reveal(user)
    await screen.findByText('359')
    act(() => {
      vi.advanceTimersByTime(CARD_DETAILS_VISIBLE_MS)
    })

    expect(screen.queryByText('359')).toBeNull()
  })
})
