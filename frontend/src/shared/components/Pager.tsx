import { Button, Group, Text } from '@mantine/core'
import { IconChevronLeft, IconChevronRight } from '@tabler/icons-react'

/** Previous / Next with "Page x of y · n things". Shared by the paged lists. */
export function Pager({ page, totalPages, total, noun, onPage }: {
  page: number; totalPages: number; total: number; noun: [string, string]; onPage: (page: number) => void
}) {
  return (
    <Group justify="space-between" mt="md" gap="sm">
      <Button variant="default" leftSection={<IconChevronLeft size={16} />} onClick={() => onPage(page - 1)} disabled={page <= 1}>
        Previous
      </Button>
      <Text size="sm" c="dimmed">
        Page {page} of {totalPages} · {total} {total === 1 ? noun[0] : noun[1]}
      </Text>
      <Button variant="default" rightSection={<IconChevronRight size={16} />} onClick={() => onPage(page + 1)} disabled={page >= totalPages}>
        Next
      </Button>
    </Group>
  )
}
