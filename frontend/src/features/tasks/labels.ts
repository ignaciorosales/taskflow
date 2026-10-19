import type { TaskSortBy, TaskStatus } from './types'

export const STATUS_LABEL: Record<TaskStatus, string> = { Todo: 'To do', InProgress: 'In progress', Done: 'Done' }

/** The board's columns, in order. */
export const STATUSES = Object.keys(STATUS_LABEL) as TaskStatus[]

export const SORT_LABEL: Record<TaskSortBy, string> = { CreatedAt: 'Creation date', DueDate: 'Due date', Priority: 'Priority' }

/** The buttons a task offers, by its current status: every other status is one button away, as the story asks. */
export const STATUS_ACTIONS: Record<TaskStatus, { to: TaskStatus; label: string }[]> = {
  Todo: [
    { to: 'InProgress', label: 'Start' },
    { to: 'Done', label: 'Complete' },
  ],
  InProgress: [
    { to: 'Done', label: 'Complete' },
    { to: 'Todo', label: 'Back to To do' },
  ],
  Done: [
    { to: 'InProgress', label: 'Reopen' },
    { to: 'Todo', label: 'Back to To do' },
  ],
}

export const formatDate = (iso: string) =>
  new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
