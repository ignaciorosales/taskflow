import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ApiError } from '../../api/client'
import { aTask, board, fakeApi, reply, session, type Reply } from '../../test/fakeApi'
import { AuthProvider, useAuth } from '../auth/AuthContext'
import type { Task, TaskQuery } from './types'
import { useTasks } from './useTasks'

function renderTasks(query: TaskQuery = {}) {
  return renderHook(({ query }) => ({ tasks: useTasks(query), auth: useAuth() }), {
    wrapper: AuthProvider,
    initialProps: { query },
  })
}

const titles = (tasks: Task[] | undefined) => tasks?.map((t) => t.title)
const input = { title: 'Buy milk', description: null, priority: 'High' as const, dueDate: null }

describe('useTasks', () => {
  it('loads one column per status, each with the filters and my access token', async () => {
    const tasks = [aTask({ title: 'A', status: 'Todo' }), aTask({ title: 'B', status: 'InProgress' }), aTask({ title: 'C', status: 'Done' })]
    const api = fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': board(tasks) })

    const { result } = renderTasks({ priority: 'High', sortBy: 'Priority', descending: true })

    await waitFor(() => expect(titles(result.current.tasks.columns?.Done.items)).toEqual(['C']))
    expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['A'])
    expect(titles(result.current.tasks.columns?.InProgress.items)).toEqual(['B'])
    const calls = api.calls('GET /api/tasks')
    expect(calls.map((c) => c.url.searchParams.get('status')).sort()).toEqual(['Done', 'InProgress', 'Todo'])
    for (const call of calls) {
      expect(Object.fromEntries(call.url.searchParams)).toMatchObject({ priority: 'High', sortBy: 'Priority', descending: 'true', page: '1', pageSize: '20' })
      expect(call.headers.get('Authorization')).toBe('Bearer token-1')
    }
  })

  it('is loading until every column arrives', async () => {
    fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': board([]) })

    const { result } = renderTasks()

    expect(result.current.tasks.isLoading).toBe(true)
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))
    expect(result.current.tasks.columns?.Todo).toEqual({ items: [], totalCount: 0 })
  })

  it('refreshes the session on a 401 and retries with the new token', async () => {
    const api = fakeApi({
      'POST /api/auth/refresh': [session('token-1'), session('token-2')],
      'GET /api/tasks': [reply(401), reply(401), reply(401), board([aTask({ title: 'A' })])],
    })

    const { result } = renderTasks()

    await waitFor(() => expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['A']))
    expect(api.calls('POST /api/auth/refresh')).toHaveLength(2)
    expect(api.calls('GET /api/tasks').slice(3).map((c) => c.headers.get('Authorization'))).toEqual(Array(3).fill('Bearer token-2'))
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

  it('reloads every column when the filters change', async () => {
    const api = fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': board([]) })
    const { result, rerender } = renderTasks({ priority: 'Low' })
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    rerender({ query: { priority: 'High' } })

    await waitFor(() => expect(api.calls('GET /api/tasks').slice(3).map((c) => c.url.searchParams.get('priority'))).toEqual(Array(3).fill('High')))
  })

  it('loads more of one column by appending its next page', async () => {
    const todo = Array.from({ length: 25 }, (_, i) => aTask({ title: `T${i + 1}` }))
    const api = fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': board(todo) })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.columns?.Todo.items).toHaveLength(20))
    expect(result.current.tasks.columns?.Todo.totalCount).toBe(25)

    await act(() => result.current.tasks.loadMore('Todo'))

    expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(titles(todo))
    const [more] = api.calls('GET /api/tasks').slice(3)
    expect(Object.fromEntries(more.url.searchParams)).toMatchObject({ status: 'Todo', page: '2' })
    expect(api.calls('GET /api/tasks')).toHaveLength(4)
  })

  it('moves a task to its new column right away, before the server answers', async () => {
    const task = aTask({ title: 'A', status: 'Todo' })
    let answer!: () => void
    const held: Reply = (call) => new Promise((resolve) => (answer = () => resolve(reply(200, { ...task, status: 'Done' })(call) as Response)))
    const api = fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': board([task]),
      [`PATCH /api/tasks/${task.id}/status`]: held,
    })
    const { result } = renderTasks()
    await waitFor(() => expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['A']))

    let moved!: Promise<void>
    act(() => {
      moved = result.current.tasks.changeStatus(task.id, 'Done')
    })

    expect(result.current.tasks.columns?.Todo.items).toEqual([])
    expect(result.current.tasks.columns?.Done.items.map((t) => [t.title, t.status])).toEqual([['A', 'Done']])
    expect(api.calls(`PATCH /api/tasks/${task.id}/status`)[0]?.body).toEqual({ status: 'Done' })
    await act(async () => {
      answer()
      await moved
    })
  })

  it('puts the task back where it was when the move is refused', async () => {
    const task = aTask({ title: 'A', status: 'Todo' })
    fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': board([task]),
      [`PATCH /api/tasks/${task.id}/status`]: reply(404, { title: 'Task not found.' }),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['A']))

    let error: unknown
    await act(async () => {
      error = await result.current.tasks.changeStatus(task.id, 'Done').catch((e: unknown) => e)
    })

    expect(error).toBeInstanceOf(ApiError)
    await waitFor(() => expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['A']))
    expect(result.current.tasks.columns?.Done.items).toEqual([])
  })

  it('creates a task and reloads the board', async () => {
    const tasks: Task[] = []
    const created = aTask({ title: 'Buy milk' })
    const api = fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': board(tasks),
      'POST /api/tasks': (call) => (tasks.push(created), reply(201, created)(call)),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    await act(() => result.current.tasks.create(input))

    expect(api.calls('POST /api/tasks')[0].body).toEqual(input)
    await waitFor(() => expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['Buy milk']))
  })

  it('rejects a create with the server validation errors so the form can show them', async () => {
    fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': board([]),
      'POST /api/tasks': reply(400, { title: 'One or more validation errors occurred.', errors: { title: ['Title is required.'] } }),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    const error = await result.current.tasks.create({ ...input, title: ' ' }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).fieldErrors).toEqual({ title: ['Title is required.'] })
    expect(result.current.auth.status).toBe('authenticated')
  })

  it('updates and deletes, reloading the board after each', async () => {
    const task = aTask()
    const api = fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': board([task]),
      [`PUT /api/tasks/${task.id}`]: reply(200, task),
      [`DELETE /api/tasks/${task.id}`]: reply(204),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))

    await act(() => result.current.tasks.update(task.id, input))
    await act(() => result.current.tasks.remove(task.id))

    expect(api.calls(`PUT /api/tasks/${task.id}`)[0].body).toEqual(input)
    expect(api.calls(`DELETE /api/tasks/${task.id}`)).toHaveLength(1)
    await waitFor(() => expect(api.calls('GET /api/tasks')).toHaveLength(9))
  })

  it('drops a Load more that answers after the filters changed', async () => {
    const todo = Array.from({ length: 25 }, (_, i) => aTask({ title: `T${i + 1}` }))
    let answer!: () => void
    const serve = board(todo)
    const api = fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': (call) =>
        call.url.searchParams.get('page') === '2' ? new Promise((resolve) => (answer = () => resolve(serve(call) as Response))) : serve(call),
    })
    const { result, rerender } = renderTasks({ priority: 'High' })
    await waitFor(() => expect(result.current.tasks.columns?.Todo.items).toHaveLength(20))

    let more!: Promise<void>
    act(() => {
      more = result.current.tasks.loadMore('Todo')
    })
    rerender({ query: { priority: 'Low' } })
    await waitFor(() => expect(api.calls('GET /api/tasks').filter((c) => c.url.searchParams.get('priority') === 'Low')).toHaveLength(3))
    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))
    await act(async () => {
      answer()
      await more
    })

    expect(result.current.tasks.columns?.Todo.items).toHaveLength(20)
  })

  it('never shows a task twice when the next page overlaps what is already loaded', async () => {
    const todo = Array.from({ length: 21 }, (_, i) => aTask({ title: `T${i + 1}` }))
    fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': board(todo) })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.columns?.Todo.items).toHaveLength(20))
    todo.unshift(aTask({ title: 'Created elsewhere' })) // shifts T20 onto page 2

    await act(() => result.current.tasks.loadMore('Todo'))

    const ids = result.current.tasks.columns!.Todo.items.map((t) => t.id)
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('keeps the pages loaded with Load more when the board reloads', async () => {
    const todo = Array.from({ length: 25 }, (_, i) => aTask({ title: `T${i + 1}` }))
    fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': board(todo),
      'POST /api/tasks': reply(201, aTask()),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(result.current.tasks.columns?.Todo.items).toHaveLength(20))
    await act(() => result.current.tasks.loadMore('Todo'))

    await act(() => result.current.tasks.create(input))

    await waitFor(() => expect(result.current.tasks.isLoading).toBe(false))
    expect(result.current.tasks.columns?.Todo.items).toHaveLength(25)
  })

  it('takes a deleted task off the board even if the reload after it fails', async () => {
    const task = aTask({ title: 'A' })
    const serve = board([task])
    let deleted = false
    fakeApi({
      'POST /api/auth/refresh': session('token-1'),
      'GET /api/tasks': (call) => (deleted ? reply(500, { title: 'Something went wrong. Please try again.' })(call) : serve(call)),
      [`DELETE /api/tasks/${task.id}`]: (call) => ((deleted = true), reply(204)(call)),
    })
    const { result } = renderTasks()
    await waitFor(() => expect(titles(result.current.tasks.columns?.Todo.items)).toEqual(['A']))

    await act(() => result.current.tasks.remove(task.id))

    await waitFor(() => expect(result.current.tasks.error).toBe('Something went wrong. Please try again.'))
    expect(result.current.tasks.columns?.Todo).toEqual({ items: [], totalCount: 0 })
  })
})
