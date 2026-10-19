import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ApiError } from '../../api/client'
import { aTask, fakeApi, pageOf, reply, session } from '../../test/fakeApi'
import { AuthProvider, useAuth } from '../auth/AuthContext'
import type { TaskQuery } from './types'
import { useTasks } from './useTasks'

function renderTasks(query: TaskQuery = {}) {
  return renderHook(({ query }) => ({ tasks: useTasks(query), auth: useAuth() }), {
    wrapper: AuthProvider,
    initialProps: { query },
  })
}

const input = { title: 'Buy milk', description: null, priority: 'High' as const, dueDate: null }

describe('useTasks', () => {
  it('loads my tasks with the filters in the query string and my access token', async () => {
    const api = fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': reply(200, pageOf(aTask())) })

    const { result } = renderTasks({ status: 'InProgress', sortBy: 'Priority', descending: true })

    await waitFor(() => expect(result.current.tasks.page?.items).toHaveLength(1))
    const [call] = api.calls('GET /api/tasks')
    expect(Object.fromEntries(call.url.searchParams)).toEqual({ status: 'InProgress', sortBy: 'Priority', descending: 'true' })
    expect(call.headers.get('Authorization')).toBe('Bearer token-1')
  })

  it('is loading until the first page arrives', async () => {
    fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': reply(200, pageOf()) })

    const { result } = renderTasks()

    expect(result.current.tasks.isLoading).toBe(true)
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))
    expect(result.current.tasks.page?.items).toEqual([])
  })

  it('refreshes the session once on a 401 and retries with the new token', async () => {
    const api = fakeApi({
      'POST /api/auth/refresh': [session('token-1'), session('token-2')],
      'GET /api/tasks': [reply(401), reply(200, pageOf(aTask()))],
    })

    const { result } = renderTasks()

    await waitFor(() => expect(result.current.tasks.page?.items).toHaveLength(1))
    expect(api.calls('GET /api/tasks').map((c) => c.headers.get('Authorization'))).toEqual(['Bearer token-1', 'Bearer token-2'])
    expect(result.current.auth.status).toBe('authenticated')
  })

  it('logs me out when the session can no longer be refreshed', async () => {
    fakeApi({
      'POST /api/auth/refresh': [session('token-1'), reply(401, { title: 'Your session has expired. Please log in again.' })],
      'GET /api/tasks': reply(401),
    })

    const { result } = renderTasks()

    await waitFor(() => expect(result.current.auth.status).toBe('anonymous'))
  })

  it('shows the server error but keeps me logged in when something else fails', async () => {
    fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': reply(500, { title: 'Something went wrong. Please try again.' }),
    })

    const { result } = renderTasks()

    await waitFor(() => expect(result.current.tasks.error).toBe('Something went wrong. Please try again.'))
    expect(result.current.tasks.isLoading).toBe(false)
    expect(result.current.auth.status).toBe('authenticated')
  })

  it('reloads when the filters change', async () => {
    const api = fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': reply(200, pageOf()) })
    const { result, rerender } = renderTasks({ status: 'Todo' })
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    rerender({ query: { status: 'Done' } })

    await waitFor(() => expect(api.calls('GET /api/tasks').at(-1)?.url.searchParams.get('status')).toBe('Done'))
  })

  it('creates a task and reloads the list', async () => {
    const created = aTask({ title: 'Buy milk' })
    const api = fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': [reply(200, pageOf()), reply(200, pageOf(created))],
      'POST /api/tasks': reply(201, created),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    await act(() => result.current.tasks.create(input))

    expect(api.calls('POST /api/tasks')[0].body).toEqual(input)
    await waitFor(() => expect(result.current.tasks.page?.items).toEqual([created]))
  })

  it('rejects a create with the server validation errors so the form can show them', async () => {
    fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': reply(200, pageOf()),
      'POST /api/tasks': reply(400, { title: 'One or more validation errors occurred.', errors: { title: ['Title is required.'] } }),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    const error = await result.current.tasks.create({ ...input, title: ' ' }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).fieldErrors).toEqual({ title: ['Title is required.'] })
    expect(result.current.auth.status).toBe('authenticated')
  })

  it('updates, changes status and deletes, reloading after each', async () => {
    const task = aTask()
    const api = fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': reply(200, pageOf(task)),
      [`PUT /api/tasks/${task.id}`]: reply(200, task),
      [`PATCH /api/tasks/${task.id}/status`]: reply(200, task),
      [`DELETE /api/tasks/${task.id}`]: reply(204),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    await act(() => result.current.tasks.update(task.id, input))
    await act(() => result.current.tasks.changeStatus(task.id, 'Done'))
    await act(() => result.current.tasks.remove(task.id))

    expect(api.calls(`PUT /api/tasks/${task.id}`)[0].body).toEqual(input)
    expect(api.calls(`PATCH /api/tasks/${task.id}/status`)[0].body).toEqual({ status: 'Done' })
    expect(api.calls(`DELETE /api/tasks/${task.id}`)).toHaveLength(1)
    await waitFor(() => expect(api.calls('GET /api/tasks')).toHaveLength(4))
  })
})
