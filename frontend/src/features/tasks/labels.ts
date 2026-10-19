import type { TaskSortBy, TaskStatus } from './types'

export const STATUS_LABEL: Record<TaskStatus, string> = { Todo: 'To do', InProgress: 'In progress', Done: 'Done' }

export const SORT_LABEL: Record<TaskSortBy, string> = { CreatedAt: 'Newest', DueDate: 'Due date', Priority: 'Priority' }

/** The buttons a task offers, by its current status. Any move is allowed; these are the useful ones. */
export const STATUS_ACTIONS: Record<TaskStatus, { to: TaskStatus; label: string }[]> = {
  Todo: [
    { to: 'InProgress', label: 'Start' },
    { to: 'Done', label: 'Complete' },
  ],
  InProgress: [
    { to: 'Done', label: 'Complete' },
    { to: 'Todo', label: 'Back to To do' },
  ],
  Done: [{ to: 'InProgress', label: 'Reopen' }],
}

export const formatDate = (iso: string) =>
  new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
