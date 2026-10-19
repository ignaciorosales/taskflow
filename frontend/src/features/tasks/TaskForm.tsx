import type { ReactNode } from 'react'
import type { Task, TaskInput } from './types'

export interface TaskFormProps {
  /** The task being edited; omit to create one. */
  initial?: Task
  onSubmit(input: TaskInput): Promise<void>
  onCancel?(): void
}

export function TaskForm(_props: TaskFormProps): ReactNode {
  return null
}
