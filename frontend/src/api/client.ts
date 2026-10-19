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

export function send(path: string, options: RequestOptions = {}, accessToken?: string | null): Promise<Response> {
  const headers: Record<string, string> = {}
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`
  if (options.json !== undefined) headers['Content-Type'] = 'application/json'
  return fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body: options.json === undefined ? undefined : JSON.stringify(options.json),
    credentials: 'same-origin',
  })
}

export async function toApiError(response: Response): Promise<ApiError> {
  const problem = (await response.json().catch(() => ({}))) as ProblemDetails
  return new ApiError(response.status, problem)
}

/** Fetch with the access token; on a 401 it refreshes once and retries, and if that fails the session is over. */
export function createApiClient({ getAccessToken, refresh, onUnauthorized }: ApiClientOptions): ApiClient {
  return {
    async request<T>(path: string, options: RequestOptions = {}): Promise<T> {
      let response = await send(path, options, getAccessToken())
      if (response.status === 401) {
        const token = await refresh()
        if (token) response = await send(path, options, token)
        if (!token || response.status === 401) {
          onUnauthorized()
          throw await toApiError(response)
        }
      }
      if (!response.ok) throw await toApiError(response)
      return (response.status === 204 ? undefined : await response.json()) as T
    },
  }
}
