import { act, cleanup, screen } from '@testing-library/react'
import { render } from '../../test/render'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { activityApi } from '../../shared/api/clients'
import { NotificationsPage } from './NotificationsPage'
import { notification, push } from './testLiveNotifications'

vi.mock('../../shared/api/clients', () => ({ activityApi: { GET: vi.fn(), POST: vi.fn() } }))
vi.mock('./liveNotifications', () => import('./testLiveNotifications'))

const get = vi.mocked(activityApi.GET)
const post = vi.mocked(activityApi.POST)
type GetResult = Awaited<ReturnType<typeof activityApi.GET>>
type PostResult = Awaited<ReturnType<typeof activityApi.POST>>

function list(items: ReturnType<typeof notification>[]): GetResult {
  return {
    data: { items, unreadCount: items.filter((n) => !n.readAt).length },
    error: undefined,
    response: new Response(null, { status: 200 }),
  } as GetResult
}

function renderPage() {
  render(<NotificationsPage />)
  return userEvent.setup()
}

describe('NotificationsPage', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
  })

  afterEach(() => {
    cleanup()
  })

  it('shows a loading state, then the empty state', async () => {
    get.mockResolvedValueOnce(list([]))
    renderPage()

    expect(screen.getByText('Loading notifications…')).toBeTruthy()
    expect(await screen.findByText(/No notifications yet/)).toBeTruthy()
  })

  it('shows each notification with the amount formatted, and read ones without a button', async () => {
    get.mockResolvedValueOnce(list([notification(2, 25000), notification(1, 1000, '2026-11-16T10:00:00Z')]))
    renderPage()

    const items = await screen.findAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]?.textContent).toContain('25,000')
    expect(items[0]?.textContent).toContain('ali')
    expect(screen.getAllByRole('button', { name: 'Mark as read' })).toHaveLength(1)
    expect(items[1]?.textContent).toContain('Read')
  })

  it('marks a notification as read, and disables the button while the request is in flight', async () => {
    get.mockResolvedValueOnce(list([notification(1, 1000)]))
    let finish: (value: PostResult) => void = () => {}
    post.mockReturnValueOnce(new Promise<PostResult>((resolve) => (finish = resolve)))
    const user = renderPage()

    await user.click(await screen.findByRole('button', { name: 'Mark as read' }))
    expect((screen.getByRole('button', { name: 'Marking…' }) as HTMLButtonElement).disabled).toBe(true)

    await act(async () =>
      finish({
        data: notification(1, 1000, '2026-11-16T10:00:00Z'),
        error: undefined,
        response: new Response(null, { status: 200 }),
      } as PostResult),
    )

    expect(post).toHaveBeenCalledWith('/api/notifications/{id}/read', { params: { path: { id: 1 } } })
    expect(screen.queryByRole('button', { name: 'Mark as read' })).toBeNull()
    expect(screen.getByText('Read')).toBeTruthy()
  })

  it('puts a live notification on top without reloading', async () => {
    get.mockResolvedValueOnce(list([notification(1, 1000)]))
    renderPage()
    await screen.findAllByRole('listitem')

    act(() => push(notification(2, 7000)))

    const items = screen.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]?.textContent).toContain('7,000')
    expect(get).toHaveBeenCalledTimes(1)
  })

  it('shows the error and loads again on "Try again"', async () => {
    get.mockResolvedValueOnce({ data: undefined, error: { status: 500, title: 'Server error' }, response: new Response(null, { status: 500 }) } as GetResult)
    get.mockResolvedValueOnce(list([]))
    const user = renderPage()

    expect((await screen.findByRole('alert')).textContent).toContain('Server error')
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText(/No notifications yet/)).toBeTruthy()
  })

  it('asks for unread only when the filter is ticked', async () => {
    get.mockResolvedValueOnce(list([notification(1, 1000)]))
    get.mockResolvedValueOnce(list([]))
    const user = renderPage()
    await screen.findAllByRole('listitem')

    await user.click(screen.getByRole('checkbox', { name: 'Unread only' }))

    expect(await screen.findByText('No unread notifications.')).toBeTruthy()
    expect(get).toHaveBeenLastCalledWith('/api/notifications', { params: { query: { unreadOnly: true } } })
  })
})
