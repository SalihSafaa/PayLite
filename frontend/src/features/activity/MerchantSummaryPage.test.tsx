import { act, cleanup, screen } from '@testing-library/react'
import { render } from '../../test/render'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { activityApi } from '../../shared/api/clients'
import { MerchantSummaryPage } from './MerchantSummaryPage'
import { notification, push } from './testLiveNotifications'
import { todayInBaghdad } from './useDailySummary'

vi.mock('../../shared/api/clients', () => ({ activityApi: { GET: vi.fn() } }))
vi.mock('./liveNotifications', () => import('./testLiveNotifications'))

const get = vi.mocked(activityApi.GET)
type GetResult = Awaited<ReturnType<typeof activityApi.GET>>

function summary(byHour: Record<number, [count: number, total: number]>): GetResult {
  const hours = Array.from({ length: 24 }, (_, hour) => ({
    hour,
    paymentCount: byHour[hour]?.[0] ?? 0,
    totalAmount: byHour[hour]?.[1] ?? 0,
  }))
  return {
    data: {
      date: todayInBaghdad(),
      currency: 'IQD',
      paymentCount: hours.reduce((n, h) => n + h.paymentCount, 0),
      totalAmount: hours.reduce((n, h) => n + h.totalAmount, 0),
      byHour: hours,
    },
    error: undefined,
    response: new Response(null, { status: 200 }),
  } as GetResult
}

describe('MerchantSummaryPage', () => {
  beforeEach(() => get.mockReset())
  afterEach(() => cleanup())

  it("loads today's summary in Baghdad time by default", async () => {
    get.mockResolvedValueOnce(summary({}))
    render(<MerchantSummaryPage />)

    expect(screen.getByText('Loading summary…')).toBeTruthy()
    expect(await screen.findByText('No payments on this day.')).toBeTruthy()
    expect(get).toHaveBeenCalledWith('/api/merchants/me/daily-summary', { params: { query: { date: todayInBaghdad() } } })
  })

  it('shows the count, the total and one bar per hour', async () => {
    get.mockResolvedValueOnce(summary({ 9: [2, 30000], 14: [1, 5000] }))
    render(<MerchantSummaryPage />)

    expect((await screen.findByLabelText('Payment count')).textContent).toBe('3')
    expect(screen.getByLabelText('Total received').textContent).toContain('35,000')
    const bars = screen.getAllByRole('listitem')
    expect(bars).toHaveLength(24)
    expect(bars[9]?.getAttribute('aria-label')).toContain('09:00 — 2 payments')
    expect(bars[14]?.getAttribute('aria-label')).toContain('1 payment,')
  })

  it("reloads when a payment arrives live and it's showing today", async () => {
    get.mockResolvedValueOnce(summary({ 9: [1, 1000] }))
    get.mockResolvedValueOnce(summary({ 9: [2, 3000] }))
    render(<MerchantSummaryPage />)
    expect((await screen.findByLabelText('Payment count')).textContent).toBe('1')

    act(() => push(notification(1, 2000)))

    await vi.waitFor(() => expect(screen.getByLabelText('Payment count').textContent).toBe('2'))
    expect(get).toHaveBeenCalledTimes(2)
  })

  it('shows the error and loads again on "Try again"', async () => {
    get.mockResolvedValueOnce({ data: undefined, error: { status: 403, title: 'Forbidden' }, response: new Response(null, { status: 403 }) } as GetResult)
    get.mockResolvedValueOnce(summary({}))
    render(<MerchantSummaryPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('Forbidden')
    await act(async () => screen.getByRole('button', { name: 'Try again' }).click())

    expect(await screen.findByText('No payments on this day.')).toBeTruthy()
  })
})
