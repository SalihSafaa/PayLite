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
    <section className="auth-card">
      <h1>Create an account</h1>
      <form onSubmit={handleSubmit} noValidate>
        <label>
          Username
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" maxLength={50} />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" maxLength={100} />
        </label>
        <label>
          Account type
          <select value={accountType} onChange={(e) => setAccountType(e.target.value as 'User' | 'Merchant')}>
            <option value="User">Personal: send and receive money</option>
            <option value="Merchant">Merchant: receive payments from customers</option>
          </select>
        </label>
        <ProblemDetailsAlert problem={problem} />
        <button type="submit" disabled={submitting}>{submitting ? 'Creating…' : 'Register'}</button>
      </form>
      <p>
        Already registered? <Link to="/login">Log in</Link>
      </p>
    </section>
  )
}
