import { useState, type ReactNode } from 'react'
import { STATUS_LABEL } from './labels'
import { DRAG_TYPE } from './TaskCard'
import type { TaskStatus } from './types'
import type { Column } from './useTasks'

interface BoardColumnProps {
  status: TaskStatus
  column: Column
  emptyText: string
  /** Called with the id of a card dropped here from another column. */
  onDropTask(id: string): void
  onLoadMore(): Promise<void>
  children: ReactNode
}

export function BoardColumn({ status, column, emptyText, onDropTask, onLoadMore, children }: BoardColumnProps) {
  const [over, setOver] = useState(false)
  const [loadingMore, setLoadingMore] = useState(false)
  const label = STATUS_LABEL[status]
  const remaining = column.totalCount - column.items.length

  return (
    <section
      className={over ? 'column drop-target' : 'column'}
      aria-label={label}
      onDragOver={(e) => {
        e.preventDefault() // allows the drop
        e.dataTransfer.dropEffect = 'move'
        setOver(true)
      }}
      onDragLeave={(e) => {
        // Moving over a card inside the column fires dragleave on the column too.
        if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setOver(false)
      }}
      onDrop={(e) => {
        e.preventDefault()
        setOver(false)
        const id = e.dataTransfer.getData(DRAG_TYPE)
        if (id && !column.items.some((t) => t.id === id)) onDropTask(id)
      }}
    >
      <h3 className="column-head">
        {label} <span className="count">{column.totalCount}</span>
      </h3>

      {column.items.length === 0 ? <p className="column-empty">{emptyText}</p> : <ul className="task-list">{children}</ul>}

      {remaining > 0 && (
        <button
          type="button"
          className="button secondary wide"
          disabled={loadingMore}
          onClick={async () => {
            setLoadingMore(true)
            await onLoadMore().finally(() => setLoadingMore(false))
          }}
        >
          {loadingMore ? 'Loading…' : `Load more (${remaining})`}
        </button>
      )}
    </section>
  )
}
