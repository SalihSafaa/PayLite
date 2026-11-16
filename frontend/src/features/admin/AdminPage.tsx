import { Badge, Button, Card, Group, SegmentedControl, Table, Text } from '@mantine/core'
import { IconCheck, IconInbox, IconX } from '@tabler/icons-react'
import { useCallback, useEffect, useState } from 'react'
import { walletApi } from '../../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../../shared/api/problemDetails'
import { PageHeader } from '../../shared/components/PageHeader'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { EmptyState, ErrorState, LoadingState } from '../../shared/components/States'
import { formatMoney } from '../../shared/lib/money'
import { TOP_UP_STATUS_COLOR, type TopUpRequest } from '../wallet/topUps'

type StatusFilter = 'Pending' | 'Approved' | 'Rejected' | 'All'
const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

function useTopUpQueue(status: StatusFilter) {
  const [data, setData] = useState<TopUpRequest[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let ignore = false
    walletApi
      .GET('/api/admin/top-ups', { params: { query: { status } } })
      .then(({ data: result, error }) => {
        if (ignore) return
        if (error || !result) {
          setProblem(toProblemDetails(error))
        } else {
          setData(result)
          setProblem(null)
        }
      })
      .catch((err: unknown) => {
        if (!ignore) setProblem(toProblemDetails(err))
      })
      .finally(() => {
        if (!ignore) setLoading(false)
      })
    return () => {
      ignore = true
    }
  }, [status, attempt])

  const reload = useCallback(() => {
    setLoading(true)
    setProblem(null)
    setAttempt((n) => n + 1)
  }, [])

  return { data, setData, loading, problem, reload }
}

/** Admin only: approve or reject top-up requests. Approving credits the wallet (once, on the server). */
export function AdminPage() {
  const [status, setStatus] = useState<StatusFilter>('Pending')
  const { data, setData, loading, problem, reload } = useTopUpQueue(status)
  const [busyId, setBusyId] = useState<number | null>(null)
  const [actionProblem, setActionProblem] = useState<ProblemDetails | null>(null)

  function changeStatus(next: string) {
    reload()
    setActionProblem(null)
    setStatus(next as StatusFilter)
  }

  async function review(request: TopUpRequest, action: 'approve' | 'reject') {
    if (busyId !== null) return // one review at a time: no double-clicks, no two buttons at once
    setBusyId(request.id)
    setActionProblem(null)
    try {
      const path = action === 'approve' ? '/api/admin/top-ups/{id}/approve' : '/api/admin/top-ups/{id}/reject'
      const { data: updated, error } = await walletApi.POST(path, { params: { path: { id: request.id } } })
      if (error || !updated) {
        // e.g. 409: another admin reviewed it first. Show why and refresh the list.
        setActionProblem(toProblemDetails(error))
        reload()
        return
      }
      setData((rows) =>
        rows === null ? rows
          : status === 'Pending' ? rows.filter((r) => r.id !== request.id) // no longer pending
          : rows.map((r) => (r.id === request.id ? updated : r)),
      )
    } catch (err) {
      setActionProblem(toProblemDetails(err))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <>
      <PageHeader title="Admin" description="Top-up requests. Approving one adds the money to that wallet." />
      <Card>
        <SegmentedControl
          mb="md"
          value={status}
          onChange={changeStatus}
          aria-label="Show"
          data={['Pending', 'Approved', 'Rejected', 'All']}
        />
        <ProblemDetailsAlert problem={actionProblem} />

        {loading && <LoadingState label="Loading top-up requests…" />}
        {!loading && problem && <ErrorState problem={problem} onRetry={reload} />}
        {!loading && !problem && data && data.length === 0 && (
          <EmptyState icon={<IconInbox size={24} stroke={1.75} />} title={status === 'Pending' ? 'Nothing to review' : 'Nothing here'}>
            {status === 'Pending' ? 'New top-up requests show up here.' : null}
          </EmptyState>
        )}
        {!loading && !problem && data && data.length > 0 && (
          <Table.ScrollContainer minWidth={720}>
            <Table verticalSpacing="sm" highlightOnHover className="data-table">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>User</Table.Th>
                  <Table.Th ta="right">Amount</Table.Th>
                  <Table.Th>Requested</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th ta="right">Action</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data.map((r) => (
                  <Table.Tr key={r.id}>
                    <Table.Td><Text fw={600} size="sm">{r.username}</Text></Table.Td>
                    <Table.Td ta="right"><Text span fw={600} className="money">{formatMoney(r.amount, r.currency)}</Text></Table.Td>
                    <Table.Td><Text size="sm" c="dimmed">{dateFormat.format(new Date(r.requestedAt))}</Text></Table.Td>
                    <Table.Td>
                      <Badge variant="light" radius="sm" color={TOP_UP_STATUS_COLOR[r.status] ?? 'gray'}>{r.status}</Badge>
                      {r.reviewedBy && <Text size="xs" c="dimmed">by {r.reviewedBy}</Text>}
                    </Table.Td>
                    <Table.Td ta="right">
                      {r.status === 'Pending' ? (
                        <Group gap="xs" justify="end" wrap="nowrap">
                          <Button size="xs" color="teal" leftSection={<IconCheck size={14} />} loading={busyId === r.id}
                            disabled={busyId !== null} onClick={() => void review(r, 'approve')} aria-label={`Approve ${r.username}'s top-up`}>
                            Approve
                          </Button>
                          <Button size="xs" variant="default" leftSection={<IconX size={14} />}
                            disabled={busyId !== null} onClick={() => void review(r, 'reject')} aria-label={`Reject ${r.username}'s top-up`}>
                            Reject
                          </Button>
                        </Group>
                      ) : (
                        <Text size="xs" c="dimmed">—</Text>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Card>
    </>
  )
}
