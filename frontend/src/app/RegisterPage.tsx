import { Anchor, Button, Container, NativeSelect, Paper, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { walletApi } from '../shared/api/clients'
import { toProblemDetails, type ProblemDetails } from '../shared/api/problemDetails'
import { ProblemDetailsAlert } from '../shared/components/ProblemDetailsAlert'

// Same rules as RegisterRequestDto in the Wallet API, checked before submit.
function validate(username: string, password: string): string | null {
  if (username.trim().length < 3 || username.trim().length > 50) return 'Username must be between 3 and 50 characters long.'
  if (password.length < 8 || password.length > 100) return 'Password must be between 8 and 100 characters long.'
  return null
}

export function RegisterPage() {
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

  return (
    <Container size={420} py="xl">
      <Title order={2} ta="center">Create an account</Title>
      <Text c="dimmed" size="sm" ta="center" mb="lg">A wallet in IQD, ready in a few seconds</Text>
      <Paper withBorder shadow="sm" p="lg" radius="md">
        <form onSubmit={handleSubmit} noValidate>
          <Stack>
            <TextInput label="Username" value={username} onChange={(e) => setUsername(e.currentTarget.value)} autoComplete="username" maxLength={50} />
            <PasswordInput label="Password" description="At least 8 characters" value={password} onChange={(e) => setPassword(e.currentTarget.value)} autoComplete="new-password" maxLength={100} />
            <NativeSelect
              label="Account type"
              value={accountType}
              onChange={(e) => setAccountType(e.currentTarget.value as 'User' | 'Merchant')}
              data={[
                { value: 'User', label: 'Personal: send and receive money' },
                { value: 'Merchant', label: 'Merchant: receive payments from customers' },
              ]}
            />
            <ProblemDetailsAlert problem={problem} />
            <Button type="submit" fullWidth loading={submitting}>Register</Button>
          </Stack>
        </form>
      </Paper>
      <Text size="sm" ta="center" mt="md">
        Already registered? <Anchor component={Link} to="/login">Log in</Anchor>
      </Text>
    </Container>
  )
}
