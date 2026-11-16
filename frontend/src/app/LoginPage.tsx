import { Anchor, Button, Container, Paper, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { walletApi } from '../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../shared/api/problemDetails'
import { useAuth } from '../shared/auth/useAuth'
import { ProblemDetailsAlert } from '../shared/components/ProblemDetailsAlert'

export function LoginPage() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!username.trim() || !password) {
      setProblem({ title: 'Enter your username and password.' })
      return
    }

    setSubmitting(true)
    setProblem(null)
    try {
      const { data, error } = await walletApi.POST('/api/auth/login', { body: { username, password } })
      if (error || !data) {
        setProblem(toProblemDetails(error))
        return
      }
      signIn(data.accessToken)
      const from = (location.state as { from?: string } | null)?.from ?? '/'
      navigate(from, { replace: true })
    } catch (err) {
      setProblem(toProblemDetails(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Container size={420} py="xl">
      <Title order={2} ta="center">Welcome back</Title>
      <Text c="dimmed" size="sm" ta="center" mb="lg">Log in to your PayLite wallet</Text>
      <Paper withBorder shadow="sm" p="lg" radius="md">
        <form onSubmit={handleSubmit} noValidate>
          <Stack>
            <TextInput label="Username" value={username} onChange={(e) => setUsername(e.currentTarget.value)} autoComplete="username" maxLength={50} />
            <PasswordInput label="Password" value={password} onChange={(e) => setPassword(e.currentTarget.value)} autoComplete="current-password" maxLength={100} />
            <ProblemDetailsAlert problem={problem} />
            <Button type="submit" fullWidth loading={submitting}>Log in</Button>
          </Stack>
        </form>
      </Paper>
      <Text size="sm" ta="center" mt="md">
        No account? <Anchor component={Link} to="/register">Register</Anchor>
      </Text>
    </Container>
  )
}
