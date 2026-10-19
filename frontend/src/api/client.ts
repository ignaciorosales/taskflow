/** The ProblemDetails body both APIs send for every error. */
export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? `Request failed (${status})`)
    this.status = status
    this.problem = problem
  }

  /** Validation messages keyed by field name, e.g. { title: ['Title is required.'] }. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {}
  }
}

export interface RequestOptions {
  method?: string
  json?: unknown
}

export interface ApiClient {
  request<T>(path: string, options?: RequestOptions): Promise<T>
}

export interface ApiClientOptions {
  getAccessToken(): string | null
  /** Gets a new access token from the refresh cookie, or null when the session is over. */
  refresh(): Promise<string | null>
  onUnauthorized(): void
}

export function createApiClient(_options: ApiClientOptions): ApiClient {
  throw new Error('Not implemented')
}
