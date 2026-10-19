import { useState } from 'react'
import { ApiError } from '../../api/client'
import { formatDate, STATUS_ACTIONS, STATUS_LABEL } from './labels'
import { TaskForm } from './TaskForm'
import type { Task, TaskInput, TaskStatus } from './types'

interface TaskCardProps {
  task: Task
  onUpdate(input: TaskInput): Promise<void>
  onChangeStatus(status: TaskStatus): Promise<void>
  onDelete(): Promise<void>
}

export function TaskCard({ task, onUpdate, onChangeStatus, onDelete }: TaskCardProps) {
  const [mode, setMode] = useState<'view' | 'edit' | 'confirm-delete'>('view')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'That didn’t work. Please try again.')
      setBusy(false)
    }
  }

  if (mode === 'edit')
    return (
      <li className="card task editing">
        <TaskForm initial={task} onSubmit={async (input) => {
          await onUpdate(input)
          setMode('view')
        }} onCancel={() => setMode('view')} />
      </li>
    )

  const classes = ['card', 'task', `status-${task.status.toLowerCase()}`, task.isOverdue && 'overdue'].filter(Boolean).join(' ')

  return (
    <li className={classes}>
      <div className="task-head">
        <h3 className="task-title">{task.title}</h3>
        <div className="badges">
          <span className={`badge priority-${task.priority.toLowerCase()}`}>{task.priority}</span>
          <span className="badge status">{STATUS_LABEL[task.status]}</span>
          {task.isOverdue && <span className="badge danger">Overdue</span>}
          {task.isCompletedLate && <span className="badge warning">Completed late</span>}
        </div>
      </div>

      {task.description && <p className="task-description">{task.description}</p>}

      <p className="task-meta">
        {task.dueDate ? <>Due {formatDate(task.dueDate)}</> : 'No due date'}
        {task.completedAt && <> · Completed {formatDate(task.completedAt)}</>}
      </p>

      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}

      {mode === 'confirm-delete' ? (
        <div className="task-actions confirm" role="group" aria-label="Confirm delete">
          <span>Delete this task?</span>
          <button type="button" className="button danger" disabled={busy} onClick={() => run(onDelete)}>
            Delete
          </button>
          <button type="button" className="button secondary" disabled={busy} onClick={() => setMode('view')}>
            Keep it
          </button>
        </div>
      ) : (
        <div className="task-actions">
          {STATUS_ACTIONS[task.status].map(({ to, label }) => (
            <button
              key={to}
              type="button"
              className="button secondary"
              disabled={busy}
              onClick={() => run(async () => {
                await onChangeStatus(to)
                setBusy(false)
              })}
            >
              {label}
            </button>
          ))}
          <span className="spacer" />
          <button type="button" className="button ghost" disabled={busy} onClick={() => setMode('edit')}>
            Edit
          </button>
          <button type="button" className="button ghost danger-text" disabled={busy} onClick={() => setMode('confirm-delete')}>
            Delete
          </button>
        </div>
      )}
    </li>
  )
}
