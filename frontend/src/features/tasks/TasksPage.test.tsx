import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { aTask, board, fakeApi, reply, session } from '../../test/fakeApi'
import { AuthProvider } from '../auth/AuthContext'
import { TasksPage } from './TasksPage'
import type { Task } from './types'

function renderPage(tasks: Task[], routes: Parameters<typeof fakeApi>[0] = {}) {
  const api = fakeApi({ 'POST /api/auth/refresh': session('token-1'), 'GET /api/tasks': board(tasks), ...routes })
  render(
    <AuthProvider>
      <TasksPage />
    </AuthProvider>,
  )
  return { api, user: userEvent.setup() }
}

const column = (name: string) => screen.getByRole('region', { name })
const card = (title: string) => screen.getByText(title).closest('li')!

// jsdom has no DataTransfer; the board only needs to carry the task id from drag to drop.
function fakeDataTransfer() {
  const data = new Map<string, string>()
  return { setData: (type: string, value: string) => data.set(type, value), getData: (type: string) => data.get(type) ?? '', effectAllowed: '', dropEffect: '' }
}

function drag(from: HTMLElement, to: HTMLElement) {
  const dataTransfer = fakeDataTransfer()
  fireEvent.dragStart(from, { dataTransfer })
  fireEvent.dragOver(to, { dataTransfer })
  fireEvent.drop(to, { dataTransfer })
  fireEvent.dragEnd(from, { dataTransfer })
}

describe('TasksPage', () => {
  it('shows a column for each status with its tasks', async () => {
    renderPage([aTask({ title: 'A', status: 'Todo' }), aTask({ title: 'B', status: 'InProgress' }), aTask({ title: 'C', status: 'Done' })])

    await waitFor(() => expect(within(column('Done')).getByText('C')).toBeInTheDocument())
    expect(within(column('To do')).getByText('A')).toBeInTheDocument()
    expect(within(column('In progress')).getByText('B')).toBeInTheDocument()
  })

  it('moves a task by dragging its card onto another column', async () => {
    const task = aTask({ title: 'A', status: 'Todo' })
    const { api } = renderPage([task], { [`PATCH /api/tasks/${task.id}/status`]: reply(200, { ...task, status: 'Done' }) })
    await screen.findByText('A')

    drag(card('A'), column('Done'))

    expect(within(column('Done')).getByText('A')).toBeInTheDocument()
    await waitFor(() => expect(api.calls(`PATCH /api/tasks/${task.id}/status`)[0]?.body).toEqual({ status: 'Done' }))
  })

  it('does nothing when a card is dropped back on its own column', async () => {
    const task = aTask({ title: 'A', status: 'Todo' })
    const { api } = renderPage([task])
    await screen.findByText('A')

    drag(card('A'), column('To do'))

    expect(within(column('To do')).getByText('A')).toBeInTheDocument()
    expect(api.calls(`PATCH /api/tasks/${task.id}/status`)).toHaveLength(0)
  })

  it('still moves a task with its status buttons, for keyboard and touch', async () => {
    const task = aTask({ title: 'A', status: 'Todo' })
    const { api, user } = renderPage([task], { [`PATCH /api/tasks/${task.id}/status`]: reply(200, { ...task, status: 'InProgress' }) })
    await screen.findByText('A')

    await user.click(within(card('A')).getByRole('button', { name: 'Start' }))

    await waitFor(() => expect(api.calls(`PATCH /api/tasks/${task.id}/status`)[0]?.body).toEqual({ status: 'InProgress' }))
  })

  it('creates a task in a dialog that closes once it is saved', async () => {
    const tasks: Task[] = []
    const created = aTask({ title: 'Buy milk' })
    const { api, user } = renderPage(tasks, { 'POST /api/tasks': (call) => (tasks.push(created), reply(201, created)(call)) })
    await waitFor(() => expect(api.calls('GET /api/tasks')).toHaveLength(3))

    await user.click(screen.getByRole('button', { name: 'New task' }))
    const dialog = screen.getByRole('dialog', { name: 'New task' })
    await user.type(within(dialog).getByLabelText('Title'), 'Buy milk')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(api.calls('POST /api/tasks')[0].body).toMatchObject({ title: 'Buy milk' })
    expect(await within(column('To do')).findByText('Buy milk')).toBeInTheDocument()
  })

  it('edits a task in the same dialog, filled in, and Cancel closes it', async () => {
    const { user } = renderPage([aTask({ title: 'A', description: 'Details' })])
    await screen.findByText('A')

    await user.click(within(card('A')).getByRole('button', { name: 'Edit' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit task' })
    expect(within(dialog).getByLabelText('Title')).toHaveValue('A')
    expect(within(dialog).getByLabelText('Description')).toHaveValue('Details')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('offers Load more only while a column has more tasks', async () => {
    const { user } = renderPage(Array.from({ length: 25 }, (_, i) => aTask({ title: `T${i + 1}` })))
    await screen.findByText('T20')
    expect(within(column('Done')).queryByRole('button', { name: /load more/i })).not.toBeInTheDocument()

    await user.click(within(column('To do')).getByRole('button', { name: /load more/i }))

    expect(await screen.findByText('T25')).toBeInTheDocument()
    expect(within(column('To do')).queryByRole('button', { name: /load more/i })).not.toBeInTheDocument()
  })

  it('ignores text dragged in from elsewhere', async () => {
    renderPage([aTask({ title: 'A' })])
    await screen.findByText('A')
    const dataTransfer = fakeDataTransfer()
    dataTransfer.setData('text/plain', 'some/selected text')

    fireEvent.dragOver(column('Done'), { dataTransfer })
    fireEvent.drop(column('Done'), { dataTransfer })

    await new Promise((resolve) => setTimeout(resolve, 50)) // room for a stray request to go out and fail
    const patches = vi.mocked(globalThis.fetch).mock.calls.filter(([, init]) => init?.method === 'PATCH')
    expect(patches).toHaveLength(0)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('moves a done task back to To do with a button', async () => {
    const task = aTask({ title: 'A', status: 'Done' })
    const { api, user } = renderPage([task], { [`PATCH /api/tasks/${task.id}/status`]: reply(200, { ...task, status: 'Todo' }) })
    await screen.findByText('A')

    await user.click(within(card('A')).getByRole('button', { name: 'Back to To do' }))

    await waitFor(() => expect(api.calls(`PATCH /api/tasks/${task.id}/status`)[0]?.body).toEqual({ status: 'Todo' }))
  })

  it('says so in the column when Load more fails', async () => {
    const serve = board(Array.from({ length: 25 }, (_, i) => aTask({ title: `T${i + 1}` })))
    const { user } = renderPage([], {
      'GET /api/tasks': (call) => (call.url.searchParams.get('page') === '2' ? reply(500, { title: 'Something went wrong.' })(call) : serve(call)),
    })
    await screen.findByText('T20')

    await user.click(within(column('To do')).getByRole('button', { name: /load more/i }))

    expect(await within(column('To do')).findByRole('alert')).toHaveTextContent("Couldn't load more tasks.")
  })

  it('keeps the dialog open when a text selection ends on the backdrop', async () => {
    const { user } = renderPage([])
    await user.click(await screen.findByRole('button', { name: 'New task' }))
    const dialog = screen.getByRole('dialog', { name: 'New task' })

    fireEvent.mouseDown(within(dialog).getByLabelText('Title'))
    fireEvent.mouseUp(dialog)
    fireEvent.click(dialog)
    expect(screen.getByRole('dialog', { name: 'New task' })).toBeInTheDocument()

    fireEvent.mouseDown(dialog)
    fireEvent.mouseUp(dialog)
    fireEvent.click(dialog)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
