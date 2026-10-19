import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { SORT_LABEL, STATUS_LABEL } from './labels'
import { TaskCard } from './TaskCard'
import { TaskForm } from './TaskForm'
import type { TaskPriority, TaskQuery, TaskSortBy, TaskStatus } from './types'
import { useTasks } from './useTasks'

const PAGE_SIZE = 10

export function TasksPage() {
  const { logout } = useAuth()
  const [query, setQuery] = useState<TaskQuery>({ sortBy: 'CreatedAt', descending: true, page: 1, pageSize: PAGE_SIZE })
  const [creating, setCreating] = useState(false)
  const { page, isLoading, error, create, update, changeStatus, remove } = useTasks(query)

  // Any filter change starts again from the first page.
  const filter = (change: Partial<TaskQuery>) => setQuery((q) => ({ ...q, ...change, page: 1 }))
  const pageCount = page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1
  const filtered = Boolean(query.status || query.priority || query.overdueOnly)

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
          {!creating && (
            <button type="button" className="button primary" onClick={() => setCreating(true)}>
              New task
            </button>
          )}
        </div>

        {creating && (
          <section className="card" aria-label="New task">
            <TaskForm
              onSubmit={async (input) => {
                await create(input)
                setCreating(false)
              }}
              onCancel={() => setCreating(false)}
            />
          </section>
        )}

        <section className="toolbar" aria-label="Filters">
          <label>
            Status
            <select value={query.status ?? ''} onChange={(e) => filter({ status: (e.target.value || undefined) as TaskStatus | undefined })}>
              <option value="">All</option>
              {(Object.keys(STATUS_LABEL) as TaskStatus[]).map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABEL[s]}
                </option>
              ))}
            </select>
          </label>
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
        </section>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        {isLoading && !page ? (
          <p className="muted" aria-busy="true">
            Loading your tasks…
          </p>
        ) : page && page.items.length === 0 ? (
          <div className="empty card">
            <p>{filtered ? 'No tasks match these filters.' : 'Nothing here yet. Create your first task.'}</p>
            {filtered && (
              <button type="button" className="button secondary" onClick={() => filter({ status: undefined, priority: undefined, overdueOnly: undefined })}>
                Clear filters
              </button>
            )}
          </div>
        ) : (
          page && (
            <ul className="task-list" aria-busy={isLoading}>
              {page.items.map((task) => (
                <TaskCard
                  key={task.id}
                  task={task}
                  onUpdate={(input) => update(task.id, input)}
                  onChangeStatus={(status) => changeStatus(task.id, status)}
                  onDelete={async () => {
                    await remove(task.id)
                    // Deleting the last task of a later page would leave an empty page behind.
                    if (page.items.length === 1 && page.page > 1) setQuery((q) => ({ ...q, page: page.page - 1 }))
                  }}
                />
              ))}
            </ul>
          )
        )}

        {page && page.totalCount > page.pageSize && (
          <nav className="pager" aria-label="Pages">
            <button type="button" className="button secondary" disabled={page.page <= 1} onClick={() => setQuery((q) => ({ ...q, page: page.page - 1 }))}>
              Previous
            </button>
            <span>
              Page {page.page} of {pageCount}
            </span>
            <button
              type="button"
              className="button secondary"
              disabled={page.page >= pageCount}
              onClick={() => setQuery((q) => ({ ...q, page: page.page + 1 }))}
            >
              Next
            </button>
          </nav>
        )}
      </main>
    </div>
  )
}
