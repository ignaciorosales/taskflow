import { useState } from 'react'
import { ApiError } from '../../api/client'
import { DeleteIcon, EditIcon, MoveIcon } from '../../components/icons'
import { formatDate, STATUS_LABEL, STATUSES } from './labels'
import type { Task, TaskStatus } from './types'

/** What a dragged card carries: a type of our own, so text or links dragged in from elsewhere are never read as a task. */
export const DRAG_TYPE = 'application/x-taskflow-task'

interface TaskCardProps {
  task: Task
  onEdit(): void
  /** Never rejects: the page shows a refused move, since the card itself jumps columns. */
  onChangeStatus(status: TaskStatus): void
  onDelete(): Promise<void>
}

export function TaskCard({ task, onEdit, onChangeStatus, onDelete }: TaskCardProps) {
  const [confirming, setConfirming] = useState(false)
  const [dragging, setDragging] = useState(false)
  const [moveMenuOpen, setMoveMenuOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function remove() {
    setBusy(true)
    setError(null)
    try {
      await onDelete()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'That didn’t work. Please try again.')
      setBusy(false)
    }
  }

  const classes = ['card', 'task', `status-${task.status.toLowerCase()}`, task.isOverdue && 'overdue', dragging && 'dragging']
    .filter(Boolean)
    .join(' ')

  return (
    <li
      className={classes}
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData(DRAG_TYPE, task.id)
        e.dataTransfer.effectAllowed = 'move'
        setDragging(true)
      }}
      onDragEnd={() => setDragging(false)}
    >
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

      {confirming ? (
        <div className="task-actions confirm" role="group" aria-label="Confirm delete">
          <span>Delete this task?</span>
          <button type="button" className="button danger" disabled={busy} onClick={remove}>
            Delete
          </button>
          <button type="button" className="button secondary" disabled={busy} onClick={() => setConfirming(false)}>
            Keep it
          </button>
        </div>
      ) : (
        <div className="task-actions icons">
          {/* Drag is the quick way to move a card; this menu is the way on touch screens and keyboards. */}
          <div
            className="move"
            onKeyDown={(e) => e.key === 'Escape' && setMoveMenuOpen(false)}
            onBlur={(e) => !e.currentTarget.contains(e.relatedTarget as Node | null) && setMoveMenuOpen(false)}
          >
            <button
              type="button"
              className="icon-button"
              aria-label="Move"
              title="Move to…"
              aria-haspopup="menu"
              aria-expanded={moveMenuOpen}
              onClick={() => setMoveMenuOpen((open) => !open)}
            >
              <MoveIcon />
            </button>
            {moveMenuOpen && (
              <div className="menu" role="menu" aria-label="Move to">
                {STATUSES.filter((s) => s !== task.status).map((to, i) => (
                  <button
                    key={to}
                    type="button"
                    role="menuitem"
                    autoFocus={i === 0}
                    onClick={() => {
                      setMoveMenuOpen(false)
                      onChangeStatus(to)
                    }}
                  >
                    {STATUS_LABEL[to]}
                  </button>
                ))}
              </div>
            )}
          </div>
          <button type="button" className="icon-button" aria-label="Edit" title="Edit" onClick={onEdit}>
            <EditIcon />
          </button>
          <button type="button" className="icon-button danger-text" aria-label="Delete" title="Delete" onClick={() => setConfirming(true)}>
            <DeleteIcon />
          </button>
        </div>
      )}
    </li>
  )
}
