import { useId, useState, type FormEvent } from 'react'
import { errorAttributes, FieldError, FormError, toFormErrors, type FieldErrors } from '../../components/formErrors'
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
  const initialDueDate = initial?.dueDate ? toLocalInput(initial.dueDate) : ''
  const [dueDate, setDueDate] = useState(initialDueDate)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
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
        // The input only shows minutes; an untouched date goes back exact, or the server sees it moved into the past.
        dueDate: dueDate === initialDueDate ? (initial?.dueDate ?? null) : dueDate ? new Date(dueDate).toISOString() : null,
      })
    } catch (error) {
      const errors = toFormErrors(error, "Couldn't save the task. Please try again.")
      setFieldErrors(errors.fieldErrors)
      setFormError(errors.formError)
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="task-form" onSubmit={handleSubmit} noValidate>
      <div className="field">
        <label htmlFor={`${id}-title`}>Title</label>
        <input id={`${id}-title`} value={title} maxLength={100} onChange={(e) => setTitle(e.target.value)} {...errorAttributes(id, 'title', fieldErrors)} />
        <FieldError formId={id} field="title" errors={fieldErrors} />
      </div>

      <div className="field">
        <label htmlFor={`${id}-description`}>Description</label>
        <textarea
          id={`${id}-description`}
          value={description}
          maxLength={1000}
          rows={3}
          onChange={(e) => setDescription(e.target.value)}
          {...errorAttributes(id, 'description', fieldErrors)}
        />
        <FieldError formId={id} field="description" errors={fieldErrors} />
      </div>

      <div className="field-row">
        <div className="field">
          <label htmlFor={`${id}-priority`}>Priority</label>
          <select
            id={`${id}-priority`}
            value={priority}
            onChange={(e) => setPriority(e.target.value as TaskPriority)}
            {...errorAttributes(id, 'priority', fieldErrors)}
          >
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
          </select>
          <FieldError formId={id} field="priority" errors={fieldErrors} />
        </div>

        <div className="field">
          <label htmlFor={`${id}-dueDate`}>Due date</label>
          <input
            id={`${id}-dueDate`}
            type="datetime-local"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            {...errorAttributes(id, 'dueDate', fieldErrors)}
          />
          <FieldError formId={id} field="dueDate" errors={fieldErrors} />
        </div>
      </div>

      <FormError message={formError} />

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
