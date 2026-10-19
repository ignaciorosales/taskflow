import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { aTask } from '../../test/fakeApi'
import { TaskForm, type TaskFormProps } from './TaskForm'
import type { TaskInput } from './types'

function renderForm(props: Partial<TaskFormProps> = {}) {
  const onSubmit = vi.fn<(input: TaskInput) => Promise<void>>().mockResolvedValue()
  render(<TaskForm onSubmit={onSubmit} {...props} />)
  return { onSubmit, user: userEvent.setup() }
}

const validationError = new ApiError(400, {
  title: 'One or more validation errors occurred.',
  errors: { title: ['Title is required.'], dueDate: ["Due date can't be in the past."] },
})

describe('TaskForm', () => {
  it('submits what the user entered, with the due date converted to UTC', async () => {
    const { onSubmit, user } = renderForm()

    await user.type(screen.getByLabelText('Title'), 'Buy milk')
    await user.type(screen.getByLabelText('Description'), 'Semi-skimmed')
    await user.selectOptions(screen.getByLabelText('Priority'), 'High')
    fireEvent.change(screen.getByLabelText('Due date'), { target: { value: '2030-01-15T10:30' } })
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(onSubmit).toHaveBeenCalledWith({
      title: 'Buy milk',
      description: 'Semi-skimmed',
      priority: 'High',
      dueDate: new Date('2030-01-15T10:30').toISOString(),
    })
  })

  it('sends empty optional fields as null and priority as Medium by default', async () => {
    const { onSubmit, user } = renderForm()

    await user.type(screen.getByLabelText('Title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(onSubmit).toHaveBeenCalledWith({ title: 'Buy milk', description: null, priority: 'Medium', dueDate: null })
  })

  it('limits title and description to what the server accepts', () => {
    renderForm()

    expect(screen.getByLabelText('Title')).toHaveAttribute('maxLength', '100')
    expect(screen.getByLabelText('Description')).toHaveAttribute('maxLength', '1000')
  })

  it('shows each server validation error next to its field', async () => {
    const { user } = renderForm({ onSubmit: vi.fn().mockRejectedValue(validationError) })

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription('Title is required.')
    expect(screen.getByLabelText('Title')).toBeInvalid()
    expect(screen.getByLabelText('Due date')).toHaveAccessibleDescription("Due date can't be in the past.")
    expect(screen.getByLabelText('Description')).toBeValid()
  })

  it('shows an error without a field as a message for the whole form', async () => {
    const { user } = renderForm({ onSubmit: vi.fn().mockRejectedValue(new ApiError(404, { title: 'Task not found.' })) })

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Task not found.')
  })

  it('shows a generic message when the request fails without a server answer', async () => {
    const { user } = renderForm({ onSubmit: vi.fn().mockRejectedValue(new TypeError('Failed to fetch')) })

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('alert')).toHaveTextContent("Couldn't save the task. Please try again.")
  })

  it('clears old errors once a save succeeds', async () => {
    const onSubmit = vi.fn().mockRejectedValueOnce(validationError).mockResolvedValueOnce(undefined)
    const { user } = renderForm({ onSubmit })

    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.type(screen.getByLabelText('Title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByLabelText('Title')).not.toHaveAccessibleDescription('Title is required.')
    expect(screen.getByLabelText('Title')).toBeValid()
  })

  it('disables Save while saving so a double click creates one task', async () => {
    let finish!: () => void
    const { user } = renderForm({ onSubmit: () => new Promise<void>((resolve) => (finish = resolve)) })

    await user.type(screen.getByLabelText('Title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(screen.getByRole('button', { name: /sav/i })).toBeDisabled()

    finish()
    expect(await screen.findByRole('button', { name: 'Save' })).toBeEnabled()
  })

  it('prefills a task being edited, showing its due date in local time', () => {
    const dueDate = '2030-01-15T09:30:00.000Z'
    renderForm({ initial: aTask({ title: 'Renew passport', description: 'New photo', priority: 'High', dueDate }) })

    const local = new Date(dueDate)
    const pad = (n: number) => String(n).padStart(2, '0')
    expect(screen.getByLabelText('Title')).toHaveValue('Renew passport')
    expect(screen.getByLabelText('Description')).toHaveValue('New photo')
    expect(screen.getByLabelText('Priority')).toHaveValue('High')
    expect(screen.getByLabelText('Due date')).toHaveValue(
      `${local.getFullYear()}-${pad(local.getMonth() + 1)}-${pad(local.getDate())}T${pad(local.getHours())}:${pad(local.getMinutes())}`,
    )
  })

  it('offers Cancel only when there is somewhere to go back to', async () => {
    const onCancel = vi.fn()
    const { user } = renderForm({ onCancel })

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalled()
  })
})
