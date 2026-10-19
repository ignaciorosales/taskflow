import { vi } from 'vitest'
import type { Task } from '../features/tasks/types'

export interface Call {
  method: string
  url: URL
  headers: Headers
  body: unknown
}

/** Builds the response for a call; it may wait, to hold a request in flight. */
export type Reply = (call: Call) => Response | Promise<Response>

export function reply(status: number, body?: unknown): Reply {
  return () =>
    body === undefined
      ? new Response(null, { status })
      : new Response(JSON.stringify(body), {
          status,
          headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
        })
}

/**
 * Replaces fetch with routes keyed "METHOD /path". A list of replies is used in order,
 * and its last reply repeats.
 */
export function fakeApi(routes: Record<string, Reply | Reply[]>) {
  const calls: Call[] = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init = {}) => {
    const url = new URL(String(input), 'http://localhost')
    const method = (init.method ?? 'GET').toUpperCase()
    const call = { method, url, headers: new Headers(init.headers), body: typeof init.body === 'string' ? JSON.parse(init.body) : undefined }
    calls.push(call)
    const route = routes[`${method} ${url.pathname}`]
    const next = Array.isArray(route) ? (route.length > 1 ? route.shift() : route[0]) : route
    if (!next) throw new Error(`Unexpected request: ${method} ${url.pathname}`)
    return next(call)
  })
  return { calls: (key: string) => calls.filter((c) => `${c.method} ${c.url.pathname}` === key) }
}

export const session = (accessToken: string) => reply(200, { accessToken, expiresAt: '2099-01-01T00:00:00Z' })

export function aTask(overrides: Partial<Task> = {}): Task {
  return {
    id: crypto.randomUUID(),
    title: 'Buy milk',
    description: null,
    priority: 'Medium',
    dueDate: null,
    status: 'Todo',
    createdAt: '2030-01-01T09:00:00.000Z',
    completedAt: null,
    isOverdue: false,
    isCompletedLate: false,
    ...overrides,
  }
}

export const pageOf = (...items: Task[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length })

/** Answers GET /api/tasks like the server: the tasks with the requested status, a page at a time. Reads the list on every call. */
export function board(tasks: Task[]): Reply {
  return (call) => {
    const params = call.url.searchParams
    const matching = tasks.filter((t) => t.status === params.get('status'))
    const page = Number(params.get('page') ?? 1)
    const pageSize = Number(params.get('pageSize') ?? 20)
    const items = matching.slice((page - 1) * pageSize, page * pageSize)
    return reply(200, { items, page, pageSize, totalCount: matching.length })(call)
  }
}
