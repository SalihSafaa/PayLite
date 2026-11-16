import { Button, Group, Text } from '@mantine/core'
import { IconChevronLeft, IconChevronRight } from '@tabler/icons-react'

/**
 * Previous / Next with "Page x of y · n things". Shared by the paged lists.
 * When everything fits on one page there is nothing to page through, so only the count is shown.
 */
export function Pager({ page, totalPages, total, noun, onPage }: {
  page: number; totalPages: number; total: number; noun: [string, string]; onPage: (page: number) => void
}) {
  const count = `${total} ${total === 1 ? noun[0] : noun[1]}`

  if (totalPages <= 1) {
    return <Text size="sm" c="dimmed" mt="md" ta="center">{count}</Text>
  }

  return (
    <Group justify="space-between" mt="md" gap="sm">
      <Button variant="default" leftSection={<IconChevronLeft size={16} />} onClick={() => onPage(page - 1)} disabled={page <= 1}>
        Previous
      </Button>
      <Text size="sm" c="dimmed">
        Page {page} of {totalPages} · {count}
      </Text>
      <Button variant="default" rightSection={<IconChevronRight size={16} />} onClick={() => onPage(page + 1)} disabled={page >= totalPages}>
        Next
      </Button>
    </Group>
  )
}
