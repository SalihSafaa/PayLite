import { createTheme } from '@mantine/core'

// One theme for the whole app (Mantine, chosen on Day 2). Kept close to the defaults on purpose.
export const theme = createTheme({
  primaryColor: 'blue',
  defaultRadius: 'md',
  fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
  headings: { fontWeight: '650' },
})
