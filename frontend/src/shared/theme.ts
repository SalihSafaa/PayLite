import { createTheme, type MantineColorsTuple } from '@mantine/core'

// Taken from the Switch app: its orange (#EF6B2D) and the deep plum of its brand material (#3B005A).
// Shade 7 of each is the one used for filled buttons and links, because it passes WCAG AA with white text
// (orange 7 = 5.2:1, plum 7 = 15.8:1). The bright Switch orange is kept for gradients and decoration.
const switchOrange: MantineColorsTuple = [
  '#FFF3EB', '#FFE3D1', '#FFC6A3', '#FDA571', '#F98647', '#F2702F', '#EF6B2D', '#C2410C', '#9A3412', '#7C2D12',
]
const plum: MantineColorsTuple = [
  '#F6F1FA', '#E9DDF3', '#D2BCE6', '#B596D6', '#9470C4', '#7350AE', '#57368F', '#3B005A', '#2E0247', '#200133',
]

export const theme = createTheme({
  primaryColor: 'switchOrange',
  primaryShade: 7,
  colors: { switchOrange, plum },
  defaultRadius: 'md',
  fontFamily: '"IBM Plex Sans", system-ui, -apple-system, "Segoe UI", sans-serif',
  headings: { fontFamily: '"IBM Plex Sans", system-ui, sans-serif', fontWeight: '600' },
  black: '#1C1427',
  components: {
    Card: { defaultProps: { radius: 'lg', withBorder: true, padding: 'lg' } },
    Button: { defaultProps: { radius: 'md' } },
    TextInput: { defaultProps: { radius: 'md', size: 'md' } },
    PasswordInput: { defaultProps: { radius: 'md', size: 'md' } },
    NativeSelect: { defaultProps: { radius: 'md', size: 'md' } },
  },
})
