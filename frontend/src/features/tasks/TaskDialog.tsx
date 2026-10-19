import { useEffect, useId, useRef } from 'react'
import { TaskForm } from './TaskForm'
import type { Task, TaskInput } from './types'

interface TaskDialogProps {
  title: string
  /** The task being edited; omit to create one. */
  initial?: Task
  onSubmit(input: TaskInput): Promise<void>
  onClose(): void
}

/** The task form in a native modal: focus is trapped, the page behind is inert, Escape closes it. */
export function TaskDialog({ title, initial, onSubmit, onClose }: TaskDialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const headingId = useId()

  // Removing the element ends the modal, so there's nothing to undo; StrictMode's second run finds it open.
  useEffect(() => {
    if (!ref.current?.open) ref.current?.showModal()
  }, [])

  return (
    <dialog
      ref={ref}
      className="dialog"
      aria-labelledby={headingId}
      onClose={onClose}
      // A click on the dialog element itself, not its content, is a click on the backdrop.
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="dialog-body">
        <h2 id={headingId}>{title}</h2>
        <TaskForm initial={initial} onSubmit={onSubmit} onCancel={onClose} />
      </div>
    </dialog>
  )
}
