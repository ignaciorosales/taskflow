import { useCallback, useEffect, useState } from 'react'
import { ApiError, type RequestOptions } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { STATUSES } from './labels'
import type { Page, Task, TaskInput, TaskQuery, TaskStatus } from './types'

export const COLUMN_PAGE_SIZE = 20

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

function columnUrl(query: TaskQuery, status: TaskStatus, page: number): string {
  const { priority, overdueOnly, sortBy, descending } = query
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries({ status, priority, overdueOnly, sortBy, descending, page, pageSize: COLUMN_PAGE_SIZE }))
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

/** The board's server data: one column per status, the filters applied to each. */
export function useTasks(query: TaskQuery): UseTasks {
  const { api, status } = useAuth()
  const [columns, setColumns] = useState<Board | null>(null)
  const [isLoading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [version, setVersion] = useState(0)
  // A string, so the effect reruns when a filter changes, not on every new query object.
  const filters = JSON.stringify([query.priority, query.overdueOnly, query.sortBy, query.descending])

  useEffect(() => {
    if (status !== 'authenticated') return
    let active = true
    setLoading(true)
    Promise.all(STATUSES.map((s) => api.request<Page<Task>>(columnUrl(query, s, 1))))
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
      const loaded = columns?.[s].items.length ?? 0
      const page = await api.request<Page<Task>>(columnUrl(query, s, Math.floor(loaded / COLUMN_PAGE_SIZE) + 1))
      setColumns((board) => board && { ...board, [s]: { items: [...board[s].items, ...page.items], totalCount: page.totalCount } })
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
    remove: (id) => mutate(`/api/tasks/${id}`, { method: 'DELETE' }),
  }
}
