export type TaskStatus = 'Todo' | 'InProgress' | 'Done'
export type TaskPriority = 'Low' | 'Medium' | 'High'
export type TaskSortBy = 'CreatedAt' | 'DueDate' | 'Priority'

export interface Task {
  id: string
  title: string
  description: string | null
  priority: TaskPriority
  dueDate: string | null
  status: TaskStatus
  createdAt: string
  completedAt: string | null
  isOverdue: boolean
  isCompletedLate: boolean
}

/** What the form sends; dates are ISO strings in UTC. */
export interface TaskInput {
  title: string
  description: string | null
  priority: TaskPriority
  dueDate: string | null
}

/** The filters and sort the board applies to every column; status and paging are per column. */
export interface TaskQuery {
  priority?: TaskPriority
  overdueOnly?: boolean
  sortBy?: TaskSortBy
  descending?: boolean
}

export interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}
