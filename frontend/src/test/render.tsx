import { MantineProvider } from '@mantine/core'
import { render as rtlRender, type RenderOptions } from '@testing-library/react'
import type { ReactElement } from 'react'
import { theme } from '../shared/theme'

/** Testing Library's render, inside the same MantineProvider the app uses. */
export function render(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
  return rtlRender(ui, { wrapper: ({ children }) => <MantineProvider theme={theme}>{children}</MantineProvider>, ...options })
}
