import type { ProblemDetails } from '../api/problemDetails'

/** The single component that displays an error from either service. */
export function ProblemDetailsAlert({ problem }: { problem: ProblemDetails | null | undefined }) {
  if (!problem) return null
  const fieldErrors = problem.errors ? Object.entries(problem.errors) : []

  return (
    <div role="alert" className="alert">
      <strong>{problem.title ?? 'Something went wrong'}</strong>
      {problem.detail && <p>{problem.detail}</p>}
      {fieldErrors.length > 0 && (
        <ul>
          {fieldErrors.flatMap(([field, messages]) =>
            messages.map((message) => <li key={`${field}-${message}`}>{message}</li>),
          )}
        </ul>
      )}
      {problem.correlationId && <small>Reference: {problem.correlationId}</small>}
    </div>
  )
}
