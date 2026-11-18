import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BellOff } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { BulkBar, Pagination } from '@/components/ui/DataTable'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Checkbox, Chip, EmptyState, PageHeader } from '@/components/ui/primitives'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { notificationKeys, notificationRequest, notificationsApi, UNREAD_COUNT_REQUEST } from './api'
import { useNotificationsText } from './i18n'
import { actionPath, FILTER_TYPES, notificationTone, notificationTypeLabel } from './labels'
import type { NotificationType } from './types'

// Design 14.20-14.21: the user's notifications from every organization, kind chips and "unread" in the address, a
// selection bar to mark read or delete.

const DEFAULT_PAGE_SIZE = 20

/** /notifications. */
export function NotificationsPage() {
  const t = useNotificationsText()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const { organizations } = useOrganization()
  const [params, setParams] = useSearchParams()
  const [selected, setSelected] = useState<number[]>([])
  const unreadOnly = params.get('unread') === '1'
  const typeParam = Number(params.get('type'))
  const type = FILTER_TYPES.includes(typeParam as NotificationType) ? (typeParam as NotificationType) : null
  const page = Math.max(1, Number(params.get('page')) || 1)
  const size = Math.max(1, Number(params.get('size')) || DEFAULT_PAGE_SIZE)
  const request = useMemo(() => notificationRequest({ unread: unreadOnly, type }, page, size), [unreadOnly, type, page, size])

  const list = useQuery({
    queryKey: notificationKeys.list(request),
    queryFn: () => notificationsApi.list(request),
    placeholderData: keepPreviousData,
  })
  const unread = useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: () => notificationsApi.list(UNREAD_COUNT_REQUEST),
  })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
  const markSeen = useMutation({
    mutationFn: (input: { ids?: number[]; all?: boolean }) => notificationsApi.markSeen(input),
    onSuccess: (_, input) => {
      showToast(input.all ? t.markedAllRead : interpolate(t.marked, { count: input.ids?.length ?? 0 }), 'success')
      setSelected([])
      refresh()
    },
  })
  const remove = useMutation({
    mutationFn: (input: { ids?: number[]; all?: boolean; count: number }) => notificationsApi.remove(input),
    onSuccess: (_, input) => {
      showToast(interpolate(t.deleted, { count: input.count }), 'success')
      setSelected([])
      refresh()
    },
  })

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null) {
        next.delete(key)
      } else {
        next.set(key, value)
      }
    }
    if (!('page' in changes)) {
      next.delete('page')
    }
    setSelected([])
    setParams(next, { replace: true })
  }

  const rows = list.data?.Data ?? []
  const total = list.data?.TotalDataCount ?? rows.length
  const unreadCount = unread.data?.TotalDataCount ?? 0
  const allSelected = rows.length > 0 && rows.every((row) => selected.includes(row.Id))
  const organizationName = (id: number | undefined) => organizations.find((item) => item.Id === id)?.Name ?? t.noOrganization

  const askDelete = async (ids: number[] | null) => {
    const count = ids ? ids.length : total
    if (
      await confirm({
        title: interpolate(ids ? t.deleteTitle : t.deleteAllTitle, { count }),
        message: t.deleteBody,
        danger: true,
        confirmLabel: t.delete,
      })
    ) {
      remove.mutate(ids ? { ids, count } : { all: true, count })
    }
  }

  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={interpolate(t.subtitle, { count: unreadCount })}
        actions={
          <>
            <Link className="btn" to="/account/notifications">
              {t.preferences}
            </Link>
            {unreadCount > 0 ? (
              <Button loading={markSeen.isPending} onClick={() => markSeen.mutate({ all: true })}>
                {t.markAllRead}
              </Button>
            ) : null}
            {total > 0 ? (
              <Button variant="danger-outline" onClick={() => void askDelete(null)}>
                {t.deleteAll}
              </Button>
            ) : null}
          </>
        }
      />
      <div className="toolbar mb-3" role="group" aria-label={t.filters}>
        <Chip selected={!unreadOnly && type === null} onClick={() => update({ unread: null, type: null })}>
          {t.filterAll}
        </Chip>
        <Chip selected={unreadOnly} onClick={() => update({ unread: unreadOnly ? null : '1' })}>
          {unreadCount > 0 ? `${t.filterUnread} · ${unreadCount}` : t.filterUnread}
        </Chip>
        {FILTER_TYPES.map((value) => (
          <Chip key={value} selected={type === value} onClick={() => update({ type: type === value ? null : String(value) })}>
            {notificationTypeLabel(value, t)}
          </Chip>
        ))}
      </div>
      {selected.length > 0 ? (
        <BulkBar count={selected.length} onClear={() => setSelected([])}>
          <Button size="sm" onClick={() => markSeen.mutate({ ids: selected })}>
            {t.markRead}
          </Button>
          <Button size="sm" variant="danger-outline" onClick={() => void askDelete(selected)}>
            {t.delete}
          </Button>
        </BulkBar>
      ) : null}
      {list.isLoading ? (
        <CardSkeleton />
      ) : list.isError ? (
        <ScreenState error={list.error} onRetry={() => void list.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<BellOff aria-hidden />} title={t.empty} description={t.emptyBody} />
      ) : (
        <div className="card relative overflow-hidden" aria-busy={list.isFetching || undefined}>
          {list.isFetching ? <div className="topline" aria-hidden /> : null}
          <div className="border-b border-border px-3 py-2">
            <Checkbox checked={allSelected} onChange={(checked) => setSelected(checked ? rows.map((row) => row.Id) : [])}>
              {t.selectAll}
            </Checkbox>
          </div>
          <ul aria-label={t.title}>
            {rows.map((notification) => {
              const target = actionPath(notification.ActionLink)
              return (
                <li
                  key={notification.Id}
                  className={cn('flex items-start gap-3 border-b border-border px-3 py-2.5', !notification.IsSeen && 'bg-bg2')}
                >
                  <Checkbox
                    checked={selected.includes(notification.Id)}
                    onChange={(checked) =>
                      setSelected((ids) => (checked ? [...ids, notification.Id] : ids.filter((id) => id !== notification.Id)))
                    }
                  >
                    <span className="sr-only">{interpolate(t.select, { text: notification.Text ?? '' })}</span>
                  </Checkbox>
                  <span className="grid min-w-0 flex-1 gap-0.5">
                    <span className="flex flex-wrap items-center gap-2">
                      <Badge tone={notificationTone(notification.Type)} small>
                        {notificationTypeLabel(notification.Type, t)}
                      </Badge>
                      <span className="text-xs text-fg3">
                        {organizationName(notification.OrganizationId)} · {format.dateTime(notification.CreationDate)}
                      </span>
                    </span>
                    <span className={cn('text-[13.5px]', !notification.IsSeen && 'font-semibold')}>{notification.Text}</span>
                    {target ? (
                      target.internal ? (
                        <Link
                          className="lnk text-[13px]"
                          to={target.href}
                          onClick={() => {
                            if (!notification.IsSeen) {
                              void notificationsApi.markSeen({ ids: [notification.Id] }).then(refresh)
                            }
                          }}
                        >
                          {t.open}
                        </Link>
                      ) : (
                        <a className="lnk text-[13px]" href={target.href} target="_blank" rel="noopener noreferrer">
                          {t.open}
                        </a>
                      )
                    ) : null}
                  </span>
                </li>
              )
            })}
          </ul>
          <Pagination
            page={page}
            pageCount={list.data?.TotalPageCount ?? 1}
            total={total}
            pageSize={size}
            onPage={(value) => update({ page: String(value) })}
            onPageSize={(value) => update({ size: String(value) })}
          />
        </div>
      )}
    </>
  )
}
