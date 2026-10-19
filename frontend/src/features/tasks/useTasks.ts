import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, type RequestOptions } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { STATUSES } from './labels'
import type { Page, Task, TaskInput, TaskQuery, TaskStatus } from './types'

export const COLUMN_PAGE_SIZE = 20
const MAX_PAGE_SIZE = 100

export interface Column {
  items: Task[]
  totalCount: number
}

export type Board = Record<TaskStatus, Column>

export interface UseTasks {
  /** Null until the first load. */
  columns: Board | null
  isLoading: boolean
  error: string | null
  loadMore(status: TaskStatus): Promise<void>
  /** Rejects with ApiError on a validation failure, so the form can show it. */
  create(input: TaskInput): Promise<void>
  update(id: string, input: TaskInput): Promise<void>
  /** Moves the card at once; if the server refuses, the board reloads and the promise rejects. */
  changeStatus(id: string, status: TaskStatus): Promise<void>
  remove(id: string): Promise<void>
}

function columnUrl(query: TaskQuery, status: TaskStatus, page: number, pageSize = COLUMN_PAGE_SIZE): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries({ status, ...query, page, pageSize }))
    if (value !== undefined) params.set(key, String(value))
  return `/api/tasks?${params}`
}

function moveCard(board: Board, id: string, to: TaskStatus): Board {
  const from = STATUSES.find((s) => board[s].items.some((t) => t.id === id))
  if (!from || from === to) return board
  const task = board[from].items.find((t) => t.id === id)!
  return {
    ...board,
    [from]: { items: board[from].items.filter((t) => t.id !== id), totalCount: board[from].totalCount - 1 },
    [to]: { items: [{ ...task, status: to }, ...board[to].items], totalCount: board[to].totalCount + 1 },
  }
}

/** Enough whole pages to show `shown` cards again, within what the API serves in one request. */
// ponytail: a column past 100 loaded cards reloads its first 100; reload page by page if that ever matters.
const reloadSize = (shown: number) => Math.min(MAX_PAGE_SIZE, Math.max(1, Math.ceil(shown / COLUMN_PAGE_SIZE)) * COLUMN_PAGE_SIZE)

function withoutCard(board: Board, id: string): Board {
  const from = STATUSES.find((s) => board[s].items.some((t) => t.id === id))
  return from ? { ...board, [from]: { items: board[from].items.filter((t) => t.id !== id), totalCount: board[from].totalCount - 1 } } : board
}

/** The board's server data: one column per status, the filters applied to each. */
export function useTasks(query: TaskQuery): UseTasks {
  const { api, status } = useAuth()
  const [columns, setColumns] = useState<Board | null>(null)
  const [isLoading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [version, setVersion] = useState(0)
  // A string, so the effect reruns when a filter changes, not on every new query object.
  const filters = JSON.stringify([query.priority, query.overdueOnly, query.sortBy, query.descending])
  // Bumped by every load, so a Load more that answers after one is dropped instead of mixing two loads.
  const generation = useRef(0)
  // What the board shows and under which filters, so a reload keeps the pages loaded with Load more.
  // Declared before the load effect, so it's current when that effect reads it.
  const shown = useRef<{ filters: string; board: Board | null }>({ filters, board: null })
  useEffect(() => {
    shown.current.board = columns
  }, [columns])

  useEffect(() => {
    if (status !== 'authenticated') return
    let active = true
    generation.current += 1
    const kept = shown.current.filters === filters ? shown.current.board : null
    shown.current.filters = filters
    setLoading(true)
    Promise.all(STATUSES.map((s) => api.request<Page<Task>>(columnUrl(query, s, 1, reloadSize(kept?.[s].items.length ?? 0)))))
      .then((pages) => {
        if (!active) return
        setColumns(Object.fromEntries(STATUSES.map((s, i) => [s, { items: pages[i].items, totalCount: pages[i].totalCount }])) as Board)
        setError(null)
      })
      .catch((e: unknown) => active && setError(e instanceof ApiError ? e.message : "Couldn't load your tasks. Please try again."))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [api, status, filters, version])

  const reload = useCallback(() => setVersion((v) => v + 1), [])

  const mutate = useCallback(
    async (path: string, options: RequestOptions) => {
      await api.request(path, options)
      reload()
    },
    [api, reload],
  )

  return {
    columns,
    isLoading,
    error,
    loadMore: async (s) => {
      const started = generation.current
      const loaded = columns?.[s].items.length ?? 0
      const page = await api.request<Page<Task>>(columnUrl(query, s, Math.floor(loaded / COLUMN_PAGE_SIZE) + 1))
      if (generation.current !== started) return
      setColumns((board) => {
        if (!board) return board
        // Tasks added or moved since the first page shift the pages, so the next one can repeat a card.
        const known = new Set(board[s].items.map((t) => t.id))
        return { ...board, [s]: { items: [...board[s].items, ...page.items.filter((t) => !known.has(t.id))], totalCount: page.totalCount } }
      })
    },
    create: (input) => mutate('/api/tasks', { method: 'POST', json: input }),
    update: (id, input) => mutate(`/api/tasks/${id}`, { method: 'PUT', json: input }),
    changeStatus: async (id, to) => {
      setColumns((board) => board && moveCard(board, id, to))
      try {
        await api.request(`/api/tasks/${id}/status`, { method: 'PATCH', json: { status: to } })
      } finally {
        // On success this picks up the server's order and completion time; on failure it puts the card back.
        reload()
      }
    },
    remove: async (id) => {
      await api.request(`/api/tasks/${id}`, { method: 'DELETE' })
      // Gone on the server, so gone from the board now, whether or not the reload works.
      setColumns((board) => board && withoutCard(board, id))
      reload()
    },
  }
}
