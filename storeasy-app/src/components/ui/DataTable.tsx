import type { ReactNode } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { PAGE_SIZES } from '@/lib/api/list'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { Select } from './Select'
import { Button } from './primitives'

export interface Column<T> {
  key: string
  header: string
  cell: (row: T) => ReactNode
  // 'main': the card title on phones; 'action': top right on phones; 'hidePhone': not shown on phones.
  role?: 'main' | 'action' | 'hidePhone'
  className?: string
}

/**
 * The kit's table (.tbl): hairlines, no shadow, text wraps instead of overflowing. Below 640 px every row turns into a
 * card where each cell carries its column name (data-l). Clickable rows open a detail.
 */
export function DataTable<T>({
  columns,
  rows,
  rowKey,
  onRowClick,
  caption,
  busy,
}: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string | number
  onRowClick?: (row: T) => void
  caption: string
  busy?: boolean
}) {
  return (
    <div className="tbl-w cards relative" aria-busy={busy || undefined}>
      {busy ? <div className="topline" aria-hidden /> : null}
      <table className="tbl">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key} scope="col" className={cn(column.role === 'action' && 'ta', column.className)}>
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)} className={onRowClick ? 'click' : undefined} onClick={onRowClick ? () => onRowClick(row) : undefined}>
              {columns.map((column) => (
                <td
                  key={column.key}
                  data-l={column.role ? undefined : column.header}
                  className={cn(
                    column.role === 'main' && 'tm',
                    column.role === 'action' && 'ta',
                    column.role === 'hidePhone' && 'tx',
                    column.className,
                  )}
                >
                  {column.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** "Page X / Y · Total: N", the page size choice and previous / next (design 20 section 5). */
export function Pagination({
  page,
  pageCount,
  total,
  pageSize,
  onPage,
  onPageSize,
  loadingNext,
}: {
  page: number
  pageCount: number
  total: number
  pageSize: number
  onPage: (page: number) => void
  onPageSize: (size: number) => void
  loadingNext?: boolean
}) {
  const t = useT()
  return (
    <nav className="pager" aria-label={t.common.pagination}>
      <span className="tabular-nums">{interpolate(t.common.pageOf, { page, pages: Math.max(1, pageCount), total })}</span>
      <span className="ml-auto hidden items-center gap-2 sm:flex">
        <span id="page-size-label">{t.common.pageSize}</span>
        <span className="w-24">
          <Select
            aria-label={t.common.pageSize}
            value={pageSize}
            options={PAGE_SIZES.map((size) => ({ value: size, label: String(size) }))}
            onChange={onPageSize}
          />
        </span>
      </span>
      <span className="flex gap-2 max-sm:ml-auto">
        <Button size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          <ChevronLeft aria-hidden />
          {t.common.previous}
        </Button>
        <Button size="sm" disabled={page >= pageCount} loading={loadingNext} onClick={() => onPage(page + 1)}>
          {t.common.next}
          <ChevronRight aria-hidden />
        </Button>
      </span>
    </nav>
  )
}

/** The inverse bar that replaces the toolbar while rows are selected (not on phones, design 19.2). */
export function BulkBar({ count, children, onClear }: { count: number; children: ReactNode; onClear: () => void }) {
  const t = useT()
  return (
    <div className="bulk max-sm:hidden" role="region" aria-label={t.common.selection}>
      <b>{interpolate(t.common.selectedCount, { count })}</b>
      {children}
      <Button size="sm" className="ml-auto" onClick={onClear}>
        {t.common.clearSelection}
      </Button>
    </div>
  )
}
