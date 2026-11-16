import { Group, Text } from '@mantine/core'
import { useId } from 'react'

/** The PayLite mark: two strokes from Switch blue into Switch orange, after the Switch app icon. */
export function BrandMark({ size = 28 }: { size?: number }) {
  // Each copy needs its own gradient id: a shared one breaks when the first copy is hidden (display: none).
  const gradientId = `pl-mark${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="0.3">
          <stop offset="0" stopColor="#215891" />
          <stop offset="0.45" stopColor="#226DA6" />
          <stop offset="0.55" stopColor="#EC7741" />
          <stop offset="1" stopColor="#EF6B2D" />
        </linearGradient>
      </defs>
      <path d="M3 6h6.5l5 13.5L19.5 6H26L17.6 27h-6.2z" fill={`url(#${gradientId})`} />
      <path d="M22.5 6H29l-4.6 11.6-3.3-8.3z" fill="#EF6B2D" />
    </svg>
  )
}

export function BrandLogo({ inverted = false }: { inverted?: boolean }) {
  return (
    <Group gap={8} wrap="nowrap">
      <BrandMark />
      <Text fw={700} fz={20} c={inverted ? 'white' : 'plum.7'} lh={1}>
        PayLite
      </Text>
    </Group>
  )
}
