import { useState } from 'react'
import { ApiError } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { BoardColumn } from './BoardColumn'
import { SORT_LABEL, STATUSES } from './labels'
import { TaskCard } from './TaskCard'
import { TaskDialog } from './TaskDialog'
import type { Task, TaskPriority, TaskQuery, TaskSortBy, TaskStatus } from './types'
import { useTasks } from './useTasks'

type Dialog = { mode: 'create' } | { mode: 'edit'; task: Task } | null

export function TasksPage() {
  const { logout } = useAuth()
  const [query, setQuery] = useState<TaskQuery>({ sortBy: 'CreatedAt', descending: true })
  const [dialog, setDialog] = useState<Dialog>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const { columns, isLoading, error, loadMore, create, update, changeStatus, remove } = useTasks(query)

  const filter = (change: Partial<TaskQuery>) => setQuery((q) => ({ ...q, ...change }))
  const filtered = Boolean(query.priority || query.overdueOnly)

  // Drag and the status buttons both end here; a refused move is already undone by the hook.
  function move(id: string, to: TaskStatus) {
    setNotice(null)
    changeStatus(id, to).catch((e: unknown) =>
      setNotice(e instanceof ApiError ? `Couldn't move the task: ${e.message}` : "Couldn't move the task. Please try again."),
    )
  }

  return (
    <div className="layout">
      <header className="topbar">
        <h1 className="brand">TaskFlow</h1>
        <button type="button" className="button ghost" onClick={logout}>
          Log out
        </button>
      </header>

      <main className="content">
        <div className="page-head">
          <h2>My tasks</h2>
          <button type="button" className="button primary" onClick={() => setDialog({ mode: 'create' })}>
            New task
          </button>
        </div>

        <section className="toolbar" aria-label="Filters">
          <label>
            Priority
            <select value={query.priority ?? ''} onChange={(e) => filter({ priority: (e.target.value || undefined) as TaskPriority | undefined })}>
              <option value="">All</option>
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </label>
          <label>
            Sort by
            <select value={query.sortBy} onChange={(e) => filter({ sortBy: e.target.value as TaskSortBy })}>
              {(Object.keys(SORT_LABEL) as TaskSortBy[]).map((s) => (
                <option key={s} value={s}>
                  {SORT_LABEL[s]}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="button secondary"
            aria-label={query.descending ? 'Sort descending' : 'Sort ascending'}
            onClick={() => filter({ descending: !query.descending })}
          >
            {query.descending ? '↓ Desc' : '↑ Asc'}
          </button>
          <label className="check">
            <input type="checkbox" checked={query.overdueOnly ?? false} onChange={(e) => filter({ overdueOnly: e.target.checked || undefined })} />
            Overdue only
          </label>
          {filtered && (
            <button type="button" className="button ghost" onClick={() => filter({ priority: undefined, overdueOnly: undefined })}>
              Clear filters
            </button>
          )}
        </section>

        {(error || notice) && (
          <p className="form-error" role="alert">
            {error ?? notice}
          </p>
        )}

        {isLoading && !columns ? (
          <p className="muted" aria-busy="true">
            Loading your tasks…
          </p>
        ) : (
          columns && (
            <div className="board" aria-busy={isLoading}>
              {STATUSES.map((status) => (
                <BoardColumn
                  key={status}
                  status={status}
                  column={columns[status]}
                  emptyText={filtered ? 'No tasks match these filters.' : 'Nothing here yet.'}
                  onDropTask={(id) => move(id, status)}
                  onLoadMore={() => loadMore(status)}
                >
                  {columns[status].items.map((task) => (
                    <TaskCard
                      key={task.id}
                      task={task}
                      onEdit={() => setDialog({ mode: 'edit', task })}
                      onChangeStatus={(to) => move(task.id, to)}
                      onDelete={() => remove(task.id)}
                    />
                  ))}
                </BoardColumn>
              ))}
            </div>
          )
        )}
      </main>

      {dialog && (
        <TaskDialog
          title={dialog.mode === 'create' ? 'New task' : 'Edit task'}
          initial={dialog.mode === 'edit' ? dialog.task : undefined}
          onSubmit={async (input) => {
            await (dialog.mode === 'create' ? create(input) : update(dialog.task.id, input))
            setDialog(null)
          }}
          onClose={() => setDialog(null)}
        />
      )}
    </div>
  )
}
