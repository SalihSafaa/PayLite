import type { components } from '../../shared/api/generated/wallet'

export type TopUpRequest = components['schemas']['TopUpRequestDto']

/** Badge colour per top-up status, the same on the dashboard and the admin page. */
export const TOP_UP_STATUS_COLOR: Record<string, string> = {
  Pending: 'switchOrange',
  Approved: 'teal',
  Rejected: 'red',
}
