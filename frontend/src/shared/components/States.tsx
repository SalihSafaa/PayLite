import { Button, Group, Loader, Stack, Text, ThemeIcon } from '@mantine/core'
import type { ReactNode } from 'react'
import type { ProblemDetails } from '../api/problemDetails'
import { ProblemDetailsAlert } from './ProblemDetailsAlert'

export function LoadingState({ label }: { label: string }) {
  return (
    <Group gap="xs" py="md">
      <Loader size="sm" color="plum.6" />
      <Text c="dimmed" size="sm">{label}</Text>
    </Group>
  )
}

export function EmptyState({ icon, title, children }: { icon: ReactNode; title: string; children?: ReactNode }) {
  return (
    <Stack align="center" gap={6} py="xl" ta="center">
      <ThemeIcon size={48} radius="xl" variant="light" color="plum">
        {icon}
      </ThemeIcon>
      <Text fw={600} mt={4}>{title}</Text>
      {children && <Text c="dimmed" size="sm" maw={360}>{children}</Text>}
    </Stack>
  )
}

export function ErrorState({ problem, onRetry }: { problem: ProblemDetails | null; onRetry: () => void }) {
  return (
    <Stack align="start" gap="sm" py="xs">
      <ProblemDetailsAlert problem={problem} />
      <Button variant="default" onClick={onRetry}>Try again</Button>
    </Stack>
  )
}
