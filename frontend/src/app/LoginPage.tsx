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
    <section className="auth-card">
      <h1>Log in</h1>
      <form onSubmit={handleSubmit} noValidate>
        <label>
          Username
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" maxLength={50} />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" maxLength={100} />
        </label>
        <ProblemDetailsAlert problem={problem} />
        <button type="submit" disabled={submitting}>{submitting ? 'Logging in…' : 'Log in'}</button>
      </form>
      <p>
        No account? <Link to="/register">Register</Link>
      </p>
    </section>
  )
}
