// RFC 7807 ProblemDetails, the error shape BOTH services return (ResultExtensions.cs,
// ExceptionHandlingMiddleware.cs, and ASP.NET's automatic 400 validation response).

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** Present on 400 validation errors: field name -> messages. */
  errors?: Record<string, string[]>
  /** Echoed by the services so a user-visible error can be found in the logs. */
  correlationId?: string
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('title' in value || 'status' in value)
}

/** Turns anything an API call can fail with into a ProblemDetails we can display. */
export function toProblemDetails(error: unknown): ProblemDetails {
  if (isProblemDetails(error)) return error
  if (error instanceof Error) return { title: 'Something went wrong', detail: error.message }
  return { title: 'Something went wrong' }
}
