import { Anchor, Button, PasswordInput, Radio, Stack, Text, TextInput } from '@mantine/core'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router'
import { walletApi } from '../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../shared/api/problemDetails'
import { useAuth } from '../shared/auth/useAuth'
import { ProblemDetailsAlert } from '../shared/components/ProblemDetailsAlert'
import { AuthLayout } from './AuthLayout'

// Same rules as RegisterRequestDto in the Wallet API, checked before submit.
function validate(username: string, password: string): string | null {
  if (username.trim().length < 3 || username.trim().length > 50) return 'Username must be between 3 and 50 characters long.'
  if (password.length < 8 || password.length > 100) return 'Password must be between 8 and 100 characters long.'
  return null
}

export function RegisterPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [accountType, setAccountType] = useState<'User' | 'Merchant'>('User')
  const [submitting, setSubmitting] = useState(false)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const validationError = validate(username, password)
    if (validationError) {
      setProblem({ title: validationError })
      return
    }

    setSubmitting(true)
    setProblem(null)
    try {
      const { error } = await walletApi.POST('/api/auth/register', { body: { username, password, accountType } })
      if (error) {
        setProblem(toProblemDetails(error))
        return
      }
      navigate('/login', { replace: true })
    } catch (err) {
      setProblem(toProblemDetails(err))
    } finally {
      setSubmitting(false)
    }
  }

  if (user) return <Navigate to="/" replace />

  return (
    <AuthLayout title="Create an account" subtitle="Your IQD wallet is ready as soon as you register.">
      <form onSubmit={handleSubmit} noValidate>
        <Stack>
          <TextInput label="Username" description="This is how people find you to send money" value={username} onChange={(e) => setUsername(e.currentTarget.value)} autoComplete="username" maxLength={50} />
          <PasswordInput label="Password" description="At least 8 characters" value={password} onChange={(e) => setPassword(e.currentTarget.value)} autoComplete="new-password" maxLength={100} />
          <Radio.Group label="Account type" value={accountType} onChange={(v) => setAccountType(v as 'User' | 'Merchant')}>
            <Stack gap="xs" mt={6}>
              <Radio.Card value="User" className="choice-card" radius="md">
                <Radio.Indicator />
                <div>
                  <Text fw={600} size="sm">Personal</Text>
                  <Text c="dimmed" size="xs">Send and receive money</Text>
                </div>
              </Radio.Card>
              <Radio.Card value="Merchant" className="choice-card" radius="md">
                <Radio.Indicator />
                <div>
                  <Text fw={600} size="sm">Merchant</Text>
                  <Text c="dimmed" size="xs">Receive payments from customers, with a daily summary</Text>
                </div>
              </Radio.Card>
            </Stack>
          </Radio.Group>
          <ProblemDetailsAlert problem={problem} />
          <Button type="submit" fullWidth size="md" loading={submitting}>Create account</Button>
        </Stack>
      </form>
      <Text size="sm" mt="lg">
        Already have an account? <Anchor component={Link} to="/login" fw={500}>Log in</Anchor>
      </Text>
    </AuthLayout>
  )
}
