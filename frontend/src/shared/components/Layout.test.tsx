import { MantineProvider } from '@mantine/core'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Role } from '../auth/jwt'
import { theme } from '../theme'
import { Layout } from './Layout'

let role: Role = 'User'
vi.mock('../auth/useAuth', () => ({
  useAuth: () => ({ user: { username: 'ali', role }, restoring: false, signIn: vi.fn(), signOut: vi.fn() }),
}))
// The bell talks to the Activity Service and SignalR; it isn't what this test is about.
vi.mock('../../features/activity/NotificationBell', () => ({ NotificationBell: () => null }))

function renderLayout() {
  render(
    <MantineProvider theme={theme}>
      <MemoryRouter>
        <Layout />
      </MemoryRouter>
    </MantineProvider>,
  )
}

describe('Layout', () => {
  afterEach(() => cleanup())

  it('shows Send money to a personal account', () => {
    role = 'User'
    renderLayout()

    expect(screen.getAllByRole('link', { name: 'Send money' }).length).toBeGreaterThan(0)
  })

  it('hides Send money from a merchant, who can only receive', () => {
    role = 'Merchant'
    renderLayout()

    expect(screen.queryByRole('link', { name: 'Send money' })).toBeNull()
    expect(screen.getAllByRole('link', { name: 'Payments' }).length).toBeGreaterThan(0)
  })
})
