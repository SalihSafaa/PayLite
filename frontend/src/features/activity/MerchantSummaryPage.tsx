import { Card, Group, SimpleGrid, Text, TextInput, Title } from '@mantine/core'
import { IconChartBar } from '@tabler/icons-react'
import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { formatMoney } from '../../shared/lib/money'
import { todayInBaghdad, useDailySummary, type DailySummary } from './useDailySummary'

export function MerchantSummaryPage() {
  const [date, setDate] = useState(todayInBaghdad)
  const { data, loading, problem, reload } = useDailySummary(date)

  function changeDate(next: string) {
    if (!next) return // the date input is empty while the user is typing
    reload()
    setDate(next)
  }

  return (
    <>
      <PageHeader
        title="Daily summary"
        description="Payments you received on one day, by hour (Baghdad time)."
        action={
          <TextInput type="date" label="Day (Baghdad time)" value={date} max={todayInBaghdad()} onChange={(e) => changeDate(e.currentTarget.value)} w={200} />
        }
      />

      {loading && <Card><LoadingState label="Loading summary…" /></Card>}

      {!loading && problem && <Card><ErrorState problem={problem} onRetry={reload} /></Card>}

      {!loading && !problem && data && data.paymentCount === 0 && (
        <Card>
          <EmptyState icon={<IconChartBar size={24} stroke={1.75} />} title="A quiet day">
            No payments on this day.
          </EmptyState>
        </Card>
      )}

      {!loading && !problem && data && data.paymentCount > 0 && <SummaryBody summary={data} />}
    </>
  )
}

function SummaryBody({ summary }: { summary: DailySummary }) {
  const max = Math.max(...summary.byHour.map((h) => h.totalAmount))
  const busiest = summary.byHour.reduce((a, b) => (b.totalAmount > a.totalAmount ? b : a))

  return (
    <>
      <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="lg" mb="lg">
        <Card>
          <Text c="dimmed" size="sm">Payments</Text>
          <Text className="stat-value" aria-label="Payment count">{summary.paymentCount}</Text>
        </Card>
        <Card>
          <Text c="dimmed" size="sm">Total</Text>
          <Text className="stat-value money" aria-label="Total received">{formatMoney(summary.totalAmount, summary.currency)}</Text>
        </Card>
        <Card>
          <Text c="dimmed" size="sm">Busiest hour</Text>
          <Text className="stat-value">{String(busiest.hour).padStart(2, '0')}:00</Text>
        </Card>
      </SimpleGrid>

      <Card>
        <Group justify="space-between" mb="md">
          <Title order={2} fz="lg">By hour</Title>
          <Text size="sm" c="dimmed">Hover a bar for details</Text>
        </Group>
        <div className="bars" role="list" aria-label="Payments by hour">
          {summary.byHour.map((h) => {
            const label = `${String(h.hour).padStart(2, '0')}:00 — ${h.paymentCount} ${h.paymentCount === 1 ? 'payment' : 'payments'}, ${formatMoney(h.totalAmount, summary.currency)}`
            return (
              <div key={h.hour} className="bar" role="listitem" aria-label={label} title={label}>
                <div className="bar-track">
                  <div className="bar-fill" style={{ height: `${max > 0 ? (h.totalAmount / max) * 100 : 0}%`, minHeight: h.totalAmount > 0 ? 3 : 0 }} />
                </div>
                <span className="bar-hour">{h.hour % 3 === 0 ? h.hour : ''}</span>
              </div>
            )
          })}
        </div>
      </Card>
    </>
  )
}
