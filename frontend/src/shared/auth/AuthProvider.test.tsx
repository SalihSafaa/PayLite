import { act, cleanup, render, screen } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../api/clients'
import { AuthProvider } from './AuthProvider'
import { tokenStore } from './tokenStore'
import { useAuth } from './useAuth'

vi.mock('../api/clients', () => ({ walletApi: { POST: vi.fn() } }))
const post = vi.mocked(walletApi.POST)

type PostResult = Awaited<ReturnType<typeof walletApi.POST>>

// A token the frontend can decode (it never checks the signature: display only).
function fakeToken(username: string, expiresInSeconds = 900): string {
  const payload = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': '1',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': username,
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'User',
    exp: Math.floor(Date.now() / 1000) + expiresInSeconds,
  }
  return `x.${btoa(JSON.stringify(payload))}.y`
}

function Who() {
  const { user, restoring } = useAuth()
  if (restoring) return <p>restoring</p>
  return <p>{user ? `user:${user.username}` : 'anonymous'}</p>
}

function renderApp() {
  render(
    <StrictMode>
      <AuthProvider>
        <Who />
      </AuthProvider>
    </StrictMode>,
  )
}

describe('AuthProvider and the refresh cookie', () => {
  beforeEach(() => {
    post.mockReset()
    tokenStore.set(null)
  })

  afterEach(() => {
    cleanup()
    vi.useRealTimers()
  })

  it('restores the session from the cookie after a page refresh, with ONE refresh call even in StrictMode', async () => {
    post.mockResolvedValue({ data: { accessToken: fakeToken('salih'), refreshToken: '' }, response: new Response() } as PostResult)

    renderApp()

    expect(screen.getByText('restoring')).toBeTruthy()
    expect(await screen.findByText('user:salih')).toBeTruthy()
    // StrictMode runs the effect twice; a second refresh with the rotated cookie would look like token theft.
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/auth/refresh', {})
  })

  it('shows the login state when there is no valid cookie', async () => {
    post.mockResolvedValue({ error: { status: 401 }, response: new Response(null, { status: 401 }) } as PostResult)

    renderApp()

    expect(await screen.findByText('anonymous')).toBeTruthy()
  })

  it('refreshes the access token shortly before it expires', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    post.mockResolvedValueOnce({ data: { accessToken: fakeToken('salih', 120), refreshToken: '' }, response: new Response() } as PostResult)
    post.mockResolvedValueOnce({ data: { accessToken: fakeToken('salih', 900), refreshToken: '' }, response: new Response() } as PostResult)

    renderApp()
    await screen.findByText('user:salih')
    expect(post).toHaveBeenCalledTimes(1)

    // The token expires in 120 s; the refresh is due 60 s before that.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(61_000)
    })
    expect(post).toHaveBeenCalledTimes(2)
    expect(screen.getByText('user:salih')).toBeTruthy()
  })
})
