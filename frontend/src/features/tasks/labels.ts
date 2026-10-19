import type { TaskSortBy, TaskStatus } from './types'

export const STATUS_LABEL: Record<TaskStatus, string> = { Todo: 'To do', InProgress: 'In progress', Done: 'Done' }

/** The board's columns, in order. */
export const STATUSES = Object.keys(STATUS_LABEL) as TaskStatus[]

export const SORT_LABEL: Record<TaskSortBy, string> = { CreatedAt: 'Creation date', DueDate: 'Due date', Priority: 'Priority' }

export const formatDate = (iso: string) =>
  new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
