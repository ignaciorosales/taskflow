import type { Page, Task, TaskInput, TaskQuery, TaskStatus } from './types'

export interface UseTasks {
  page: Page<Task> | null
  isLoading: boolean
  error: string | null
  /** Rejects with ApiError on a validation failure, so the form can show it. */
  create(input: TaskInput): Promise<void>
  update(id: string, input: TaskInput): Promise<void>
  changeStatus(id: string, status: TaskStatus): Promise<void>
  remove(id: string): Promise<void>
}

export function useTasks(_query: TaskQuery): UseTasks {
  throw new Error('Not implemented')
}
