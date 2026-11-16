import { MantineProvider } from '@mantine/core'
import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { theme } from '../../shared/theme'
import type { TopUpRequest } from '../wallet/topUps'
import { AdminPage } from './AdminPage'

vi.mock('../../shared/api/clients', () => ({ walletApi: { GET: vi.fn(), POST: vi.fn() } }))
const get = vi.mocked(walletApi.GET)
const post = vi.mocked(walletApi.POST)
type GetResult = Awaited<ReturnType<typeof walletApi.GET>>
type PostResult = Awaited<ReturnType<typeof walletApi.POST>>

function request(id: number, username: string, status = 'Pending'): TopUpRequest {
  return { id, username, amount: 50_000, currency: 'IQD', status, requestedAt: '2026-11-16T09:00:00Z' }
}

const list = (items: TopUpRequest[]) => ({ data: items, error: undefined, response: new Response() }) as GetResult

function renderPage() {
  render(
    <MantineProvider theme={theme}>
      <AdminPage />
    </MantineProvider>,
  )
  return userEvent.setup()
}

describe('AdminPage', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
  })
  afterEach(() => cleanup())

  it('lists the pending requests with the money formatted', async () => {
    get.mockResolvedValueOnce(list([request(1, 'ali'), request(2, 'sara')]))
    renderPage()

    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2
    expect(rows[1]?.textContent).toContain('ali')
    expect(rows[1]?.textContent).toContain('50,000')
    expect(get).toHaveBeenCalledWith('/api/admin/top-ups', { params: { query: { status: 'Pending' } } })
  })

  it('approves a request, and takes it off the pending list', async () => {
    get.mockResolvedValueOnce(list([request(1, 'ali'), request(2, 'sara')]))
    post.mockResolvedValueOnce({ data: { ...request(1, 'ali', 'Approved'), balanceAfter: 50_000 }, error: undefined, response: new Response() } as PostResult)
    const user = renderPage()

    await user.click(await screen.findByRole('button', { name: "Approve ali's top-up" }))

    expect(post).toHaveBeenCalledWith('/api/admin/top-ups/{id}/approve', { params: { path: { id: 1 } } })
    await vi.waitFor(() => expect(screen.queryByText('ali')).toBeNull())
    expect(screen.getByText('sara')).toBeTruthy()
  })

  it('disables every action while one review is in flight (no double approve)', async () => {
    get.mockResolvedValueOnce(list([request(1, 'ali'), request(2, 'sara')]))
    let finish: (r: PostResult) => void = () => {}
    post.mockReturnValueOnce(new Promise<PostResult>((resolve) => (finish = resolve)))
    const user = renderPage()

    const approve = await screen.findByRole('button', { name: "Approve ali's top-up" })
    await user.dblClick(approve)

    expect(post).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: "Reject sara's top-up" }).hasAttribute('disabled')).toBe(true)
    await act(async () => finish({ data: request(1, 'ali', 'Approved'), error: undefined, response: new Response() } as PostResult))
  })

  it('shows why when another admin got there first, and reloads', async () => {
    get.mockResolvedValueOnce(list([request(1, 'ali')]))
    get.mockResolvedValueOnce(list([]))
    post.mockResolvedValueOnce({ data: undefined, error: { status: 409, title: 'This top-up was already approved.' }, response: new Response(null, { status: 409 }) } as PostResult)
    const user = renderPage()

    await user.click(await screen.findByRole('button', { name: "Approve ali's top-up" }))

    expect((await screen.findByRole('alert')).textContent).toContain('already approved')
    expect(await screen.findByText('Nothing to review')).toBeTruthy()
  })

  it('shows reviewed requests when the filter changes, without action buttons', async () => {
    get.mockResolvedValueOnce(list([]))
    get.mockResolvedValueOnce(list([{ ...request(3, 'ali', 'Approved'), reviewedBy: 'boss' }]))
    const user = renderPage()
    await screen.findByText('Nothing to review')

    await user.click(screen.getByRole('radio', { name: 'Approved' }))

    expect(await screen.findByText('by boss')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Approve/ })).toBeNull()
    expect(get).toHaveBeenLastCalledWith('/api/admin/top-ups', { params: { query: { status: 'Approved' } } })
  })
})
