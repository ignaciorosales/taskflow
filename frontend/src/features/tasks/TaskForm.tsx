import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import type { Task, TaskInput, TaskPriority } from './types'

export interface TaskFormProps {
  /** The task being edited; omit to create one. */
  initial?: Task
  onSubmit(input: TaskInput): Promise<void>
  onCancel?(): void
}

const pad = (n: number) => String(n).padStart(2, '0')

/** ISO instant → the "yyyy-MM-ddTHH:mm" a datetime-local input shows, in the browser's time zone. */
function toLocalInput(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function TaskForm({ initial, onSubmit, onCancel }: TaskFormProps) {
  const id = useId()
  const [title, setTitle] = useState(initial?.title ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [priority, setPriority] = useState<TaskPriority>(initial?.priority ?? 'Medium')
  const [dueDate, setDueDate] = useState(initial?.dueDate ? toLocalInput(initial.dueDate) : '')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setFieldErrors({})
    setFormError(null)
    try {
      await onSubmit({
        title,
        description: description.trim() ? description : null,
        priority,
        dueDate: dueDate ? new Date(dueDate).toISOString() : null,
      })
    } catch (error) {
      if (!(error instanceof ApiError)) setFormError("Couldn't save the task. Please try again.")
      else if (Object.keys(error.fieldErrors).length > 0) setFieldErrors(error.fieldErrors)
      else setFormError(error.message)
    } finally {
      setSaving(false)
    }
  }

  // Server messages sit under their input and are read out with it.
  const errorProps = (field: string) =>
    fieldErrors[field]
      ? { 'aria-invalid': true, 'aria-describedby': `${id}-${field}-error` }
      : {}
  const errorText = (field: string) =>
    fieldErrors[field] && (
      <p className="field-error" id={`${id}-${field}-error`}>
        {fieldErrors[field].join(' ')}
      </p>
    )

  return (
    <form className="task-form" onSubmit={handleSubmit} noValidate>
      <div className="field">
        <label htmlFor={`${id}-title`}>Title</label>
        <input id={`${id}-title`} value={title} maxLength={100} onChange={(e) => setTitle(e.target.value)} {...errorProps('title')} />
        {errorText('title')}
      </div>

      <div className="field">
        <label htmlFor={`${id}-description`}>Description</label>
        <textarea
          id={`${id}-description`}
          value={description}
          maxLength={1000}
          rows={3}
          onChange={(e) => setDescription(e.target.value)}
          {...errorProps('description')}
        />
        {errorText('description')}
      </div>

      <div className="field-row">
        <div className="field">
          <label htmlFor={`${id}-priority`}>Priority</label>
          <select
            id={`${id}-priority`}
            value={priority}
            onChange={(e) => setPriority(e.target.value as TaskPriority)}
            {...errorProps('priority')}
          >
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
          </select>
          {errorText('priority')}
        </div>

        <div className="field">
          <label htmlFor={`${id}-dueDate`}>Due date</label>
          <input
            id={`${id}-dueDate`}
            type="datetime-local"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            {...errorProps('dueDate')}
          />
          {errorText('dueDate')}
        </div>
      </div>

      {formError && (
        <p className="form-error" role="alert">
          {formError}
        </p>
      )}

      <div className="form-actions">
        {onCancel && (
          <button type="button" className="button secondary" onClick={onCancel}>
            Cancel
          </button>
        )}
        <button type="submit" className="button primary" disabled={saving}>
          {saving ? 'Saving…' : 'Save'}
        </button>
      </div>
    </form>
  )
}
