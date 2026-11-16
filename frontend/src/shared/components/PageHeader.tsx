import { Group, Stack, Text, Title } from '@mantine/core'
import type { ReactNode } from 'react'

/** Title, one line of context and an optional action, the same on every page. */
export function PageHeader({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <Group justify="space-between" align="end" mb="lg" wrap="wrap" gap="sm">
      <Stack gap={4}>
        <Title order={1} fz={{ base: 26, sm: 30 }}>{title}</Title>
        {description && <Text c="dimmed">{description}</Text>}
      </Stack>
      {action}
    </Group>
  )
}
