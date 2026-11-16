import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { MerchantPaymentsPage } from './MerchantPaymentsPage'
import type { MerchantPayment } from './useMerchantPayments'

vi.mock('../../shared/api/clients', () => ({ walletApi: { GET: vi.fn() } }))
vi.mock('./liveNotifications', () => import('./testLiveNotifications'))

const get = vi.mocked(walletApi.GET)
type GetResult = Awaited<ReturnType<typeof walletApi.GET>>

const payment = (transferId: number): MerchantPayment => ({
  transferId,
  fromUsername: 'ali',
  amount: 25000,
  currency: 'IQD',
  createdAt: '2026-11-16T09:15:00Z',
})

function page(items: MerchantPayment[], totalCount: number, pageNumber = 1): GetResult {
  return {
    data: { items, totalCount, page: pageNumber, pageSize: 10 },
    error: undefined,
    response: new Response(null, { status: 200 }),
  } as GetResult
}

describe('MerchantPaymentsPage', () => {
  beforeEach(() => get.mockReset())
  afterEach(() => cleanup())

  it('shows a loading state, then the empty state', async () => {
    get.mockResolvedValueOnce(page([], 0))
    render(<MerchantPaymentsPage />)

    expect(screen.getByText('Loading payments…')).toBeTruthy()
    expect(await screen.findByText(/No payments yet/)).toBeTruthy()
  })

  it('shows the payments with the money formatted', async () => {
    get.mockResolvedValueOnce(page([payment(2), payment(1)], 2))
    render(<MerchantPaymentsPage />)

    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2
    expect(rows[1]?.textContent).toContain('ali')
    expect(rows[1]?.textContent).toContain('25,000')
    expect(screen.getByText(/Page 1 of 1 · 2 payments/)).toBeTruthy()
  })

  it('pages through the payments', async () => {
    get.mockResolvedValueOnce(page([payment(12)], 12))
    get.mockResolvedValueOnce(page([payment(2)], 12, 2))
    const user = userEvent.setup()
    render(<MerchantPaymentsPage />)

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    expect(await screen.findByText(/Page 2 of 2/)).toBeTruthy()
    expect(get).toHaveBeenLastCalledWith('/api/merchants/me/payments', { params: { query: { page: 2, pageSize: 10 } } })
  })

  it('shows the error and loads again on "Try again"', async () => {
    get.mockResolvedValueOnce({ data: undefined, error: { status: 500, title: 'Server error' }, response: new Response(null, { status: 500 }) } as GetResult)
    get.mockResolvedValueOnce(page([], 0))
    const user = userEvent.setup()
    render(<MerchantPaymentsPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('Server error')
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText(/No payments yet/)).toBeTruthy()
  })
})
