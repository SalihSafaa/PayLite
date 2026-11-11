// Reads the claims the Wallet API puts in the token (see JwtTokenService.cs).
// This is for DISPLAY only (name, role in the UI). The server always re-checks the token.

export type Role = 'User' | 'Merchant' | 'Admin'

export interface CurrentUser {
  id: string
  username: string
  role: Role
  expiresAt: Date
}

// JwtTokenService writes the full .NET claim-type URIs (ClaimTypes.NameIdentifier, ...).
const CLAIM_ID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
const CLAIM_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'
const CLAIM_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

interface WalletJwtPayload {
  [CLAIM_ID]: string
  [CLAIM_NAME]: string
  [CLAIM_ROLE]: Role
  exp: number
}

export function decodeUser(token: string): CurrentUser {
  const payloadPart = token.split('.')[1]
  if (!payloadPart) throw new Error('Malformed token')
  const json = atob(payloadPart.replace(/-/g, '+').replace(/_/g, '/'))
  const payload = JSON.parse(json) as WalletJwtPayload
  return {
    id: payload[CLAIM_ID],
    username: payload[CLAIM_NAME],
    role: payload[CLAIM_ROLE],
    expiresAt: new Date(payload.exp * 1000),
  }
}
