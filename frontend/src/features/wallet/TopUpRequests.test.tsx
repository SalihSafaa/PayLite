import { MantineProvider } from '@mantine/core'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { theme } from '../../shared/theme'
import { TopUpForm } from './TopUpForm'
import { TopUpRequests } from './TopUpRequests'
import type { TopUpRequest } from './topUps'

vi.mock('../../shared/api/clients', () => ({ walletApi: { POST: vi.fn() } }))
const post = vi.mocked(walletApi.POST)
type PostResult = Awaited<ReturnType<typeof walletApi.POST>>

const pending: TopUpRequest = { id: 9, username: 'ali', amount: 25_000, currency: 'IQD', status: 'Pending', requestedAt: '2026-11-16T09:00:00Z' }

const wrap = (ui: React.ReactNode) => render(<MantineProvider theme={theme}>{ui}</MantineProvider>)

describe('Top-up requests', () => {
  beforeEach(() => post.mockReset())
  afterEach(() => cleanup())

  it('the form requests a top-up and hands back the Pending request', async () => {
    post.mockResolvedValueOnce({ data: pending, error: undefined, response: new Response(null, { status: 202 }) } as PostResult)
    const onRequested = vi.fn()
    wrap(<TopUpForm onRequested={onRequested} />)
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('Amount (IQD)'), '25000')
    await user.click(screen.getByRole('button', { name: 'Request top-up' }))

    await vi.waitFor(() => expect(onRequested).toHaveBeenCalledWith(pending))
    expect(post).toHaveBeenCalledWith('/api/wallets/me/top-ups', { body: { amount: 25_000 } })
    expect(screen.getByText(/An admin checks every top-up/)).toBeTruthy()
  })

  it('shows each request with its status', () => {
    wrap(
      <TopUpRequests
        data={[pending, { ...pending, id: 8, status: 'Approved' }, { ...pending, id: 7, status: 'Rejected' }]}
        loading={false}
        problem={null}
        onRetry={() => {}}
      />,
    )

    expect(screen.getByText('Pending')).toBeTruthy()
    expect(screen.getByText('Approved')).toBeTruthy()
    expect(screen.getByText('Rejected')).toBeTruthy()
    expect(screen.getAllByText(/25,000/)).toHaveLength(3)
  })

  it('has an empty state', () => {
    wrap(<TopUpRequests data={[]} loading={false} problem={null} onRetry={() => {}} />)

    expect(screen.getByText('No top-ups yet')).toBeTruthy()
  })
})
