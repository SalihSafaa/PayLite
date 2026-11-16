import { Anchor, Button, PasswordInput, Stack, Text, TextInput } from '@mantine/core'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { walletApi } from '../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../shared/api/problemDetails'
import { useAuth } from '../shared/auth/useAuth'
import { ProblemDetailsAlert } from '../shared/components/ProblemDetailsAlert'
import { AuthLayout } from './AuthLayout'

export function LoginPage() {
  const { user, signIn } = useAuth()
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

  // Already logged in (e.g. back button or a bookmark): go to the wallet instead.
  if (user) return <Navigate to="/" replace />

  return (
    <AuthLayout title="Log in" subtitle="Welcome back. Log in to your wallet.">
      <form onSubmit={handleSubmit} noValidate>
        <Stack>
          <TextInput label="Username" value={username} onChange={(e) => setUsername(e.currentTarget.value)} autoComplete="username" maxLength={50} />
          <PasswordInput label="Password" value={password} onChange={(e) => setPassword(e.currentTarget.value)} autoComplete="current-password" maxLength={100} />
          <ProblemDetailsAlert problem={problem} />
          <Button type="submit" fullWidth size="md" loading={submitting}>Log in</Button>
        </Stack>
      </form>
      <Text size="sm" mt="lg">
        New to PayLite? <Anchor component={Link} to="/register" fw={500}>Create an account</Anchor>
      </Text>
    </AuthLayout>
  )
}
