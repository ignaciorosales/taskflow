import { useCallback, useEffect, useState } from 'react'
import { ApiError, type RequestOptions } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import type { Page, Task, TaskInput, TaskQuery, TaskStatus } from './types'

export interface UseTasks {
  page: Page<Task> | null
  isLoading: boolean
  error: string | null
  /** Rejects with ApiError on a validation failure, so the form can show it. */
  create(input: TaskInput): Promise<void>
  update(id: string, input: TaskInput): Promise<void>
  changeStatus(id: string, status: TaskStatus): Promise<void>
  remove(id: string): Promise<void>
}

function toSearch(query: TaskQuery): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query))
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  const search = params.toString()
  return search ? `?${search}` : ''
}

export function useTasks(query: TaskQuery): UseTasks {
  const { api, status } = useAuth()
  const [page, setPage] = useState<Page<Task> | null>(null)
  const [isLoading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [version, setVersion] = useState(0)
  const search = toSearch(query)

  useEffect(() => {
    if (status !== 'authenticated') return
    let active = true
    setLoading(true)
    api
      .request<Page<Task>>(`/api/tasks${search}`)
      .then((result) => {
        if (!active) return
        setPage(result)
        setError(null)
      })
      .catch((e: unknown) => active && setError(e instanceof ApiError ? e.message : "Couldn't load your tasks. Please try again."))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [api, status, search, version])

  const mutate = useCallback(
    async (path: string, options: RequestOptions) => {
      await api.request(path, options)
      setVersion((v) => v + 1)
    },
    [api],
  )

  return {
    page,
    isLoading,
    error,
    create: (input) => mutate('/api/tasks', { method: 'POST', json: input }),
    update: (id, input) => mutate(`/api/tasks/${id}`, { method: 'PUT', json: input }),
    changeStatus: (id, newStatus) => mutate(`/api/tasks/${id}/status`, { method: 'PATCH', json: { status: newStatus } }),
    remove: (id) => mutate(`/api/tasks/${id}`, { method: 'DELETE' }),
  }
}
