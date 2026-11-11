import createClient, { type Middleware } from 'openapi-fetch'
import { tokenStore } from '../auth/tokenStore'
import type { paths as ActivityPaths } from './generated/activity'
import type { paths as WalletPaths } from './generated/wallet'

// Typed clients generated from each service's OpenAPI document.
// Never hand-write request/response types: run `npm run gen:api` after an API change.

const CORRELATION_HEADER = 'X-Correlation-ID' // keep in sync with Shared.Contracts/Http/CorrelationHeaders.cs

const commonHeaders: Middleware = {
  onRequest({ request }) {
    const token = tokenStore.get()
    if (token) request.headers.set('Authorization', `Bearer ${token}`)
    // One id per user action, traced through Wallet API -> Kafka -> Activity Service logs.
    if (!request.headers.has(CORRELATION_HEADER)) {
      request.headers.set(CORRELATION_HEADER, crypto.randomUUID())
    }
    return request
  },
}

// Both base URLs are same-origin: the Vite dev proxy / nginx route /api/... to the right service.
export const walletApi = createClient<WalletPaths>({ baseUrl: '' })
walletApi.use(commonHeaders)

export const activityApi = createClient<ActivityPaths>({ baseUrl: '' })
activityApi.use(commonHeaders)
