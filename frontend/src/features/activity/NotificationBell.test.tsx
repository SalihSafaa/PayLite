import { act, cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { activityApi } from '../../shared/api/clients'
import { NotificationBell } from './NotificationBell'
import { notification, notifyNotificationsChanged, push } from './testLiveNotifications'

vi.mock('../../shared/api/clients', () => ({ activityApi: { GET: vi.fn() } }))
vi.mock('./liveNotifications', () => import('./testLiveNotifications'))

const get = vi.mocked(activityApi.GET)
type GetResult = Awaited<ReturnType<typeof activityApi.GET>>

const count = (unreadCount: number) =>
  ({ data: { items: [], unreadCount }, error: undefined, response: new Response(null, { status: 200 }) }) as GetResult

function renderBell() {
  render(
    <MemoryRouter>
      <NotificationBell />
    </MemoryRouter>,
  )
}

describe('NotificationBell', () => {
  beforeEach(() => get.mockReset())
  afterEach(() => cleanup())

  it('shows the unread count from the server', async () => {
    get.mockResolvedValueOnce(count(3))
    renderBell()

    expect(await screen.findByLabelText('Notifications, 3 unread')).toBeTruthy()
  })

  it('shows no badge when everything is read', async () => {
    get.mockResolvedValueOnce(count(0))
    renderBell()

    await vi.waitFor(() => expect(get).toHaveBeenCalled())
    expect(screen.getByLabelText('Notifications')).toBeTruthy()
  })

  it('goes up by one for every live notification, without asking the server', async () => {
    get.mockResolvedValueOnce(count(1))
    renderBell()
    await screen.findByLabelText('Notifications, 1 unread')

    act(() => push(notification(5, 1000)))
    act(() => push(notification(6, 1000)))

    expect(screen.getByLabelText('Notifications, 3 unread')).toBeTruthy()
    expect(get).toHaveBeenCalledTimes(1)
  })

  it('reloads the count when something is marked as read', async () => {
    get.mockResolvedValueOnce(count(2))
    get.mockResolvedValueOnce(count(1))
    renderBell()
    await screen.findByLabelText('Notifications, 2 unread')

    act(() => notifyNotificationsChanged())

    expect(await screen.findByLabelText('Notifications, 1 unread')).toBeTruthy()
  })
})
