import { Card, Group, Table, Text, ThemeIcon } from '@mantine/core'
import { IconArrowDownLeft, IconReceipt } from '@tabler/icons-react'
import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Pager } from '../../shared/components/Pager'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { formatMoney } from '../../shared/lib/money'
import { useMerchantPayments } from './useMerchantPayments'

const PAGE_SIZE = 10
const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

export function MerchantPaymentsPage() {
  const [page, setPage] = useState(1)
  const { data, loading, problem, reload } = useMerchantPayments(page, PAGE_SIZE)

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  function goTo(next: number) {
    reload()
    setPage(next)
  }

  return (
    <>
      <PageHeader title="Incoming payments" description="Payments from your customers, newest first." />
      <Card>
        {loading && <LoadingState label="Loading payments…" />}

        {!loading && problem && <ErrorState problem={problem} onRetry={reload} />}

        {!loading && !problem && data && data.totalCount === 0 && (
          <EmptyState icon={<IconReceipt size={24} stroke={1.75} />} title="Waiting for your first customer">
            No payments yet. Payments from customers show up here.
          </EmptyState>
        )}

        {!loading && !problem && data && data.totalCount > 0 && (
          <>
            <Table.ScrollContainer minWidth={480}>
              <Table verticalSpacing="md" highlightOnHover className="data-table">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>From</Table.Th>
                    <Table.Th>Date</Table.Th>
                    <Table.Th ta="right">Amount</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {data.items.map((p) => (
                    <Table.Tr key={p.transferId}>
                      <Table.Td>
                        <Group gap="sm" wrap="nowrap">
                          <ThemeIcon size={36} radius="xl" variant="light" color="teal" aria-hidden="true">
                            <IconArrowDownLeft size={18} stroke={1.75} />
                          </ThemeIcon>
                          <Text fw={600} size="sm">{p.fromUsername}</Text>
                        </Group>
                      </Table.Td>
                      <Table.Td style={{ whiteSpace: 'nowrap' }}>
                        <Text size="sm" c="dimmed">{dateFormat.format(new Date(p.createdAt))}</Text>
                      </Table.Td>
                      <Table.Td ta="right" style={{ whiteSpace: 'nowrap' }}>
                        <Text span fw={600} c="teal.8" className="money">+{formatMoney(p.amount, p.currency)}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
            <Pager page={page} totalPages={totalPages} total={data.totalCount} noun={['payment', 'payments']} onPage={goTo} />
          </>
        )}
      </Card>
    </>
  )
}
