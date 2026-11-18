import { useMemo, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MessageSquare, RefreshCw, Star } from 'lucide-react'
import { Badge, StoreTag, type BadgeTone } from '@/components/ui/Badge'
import { Pagination } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Menu, type MenuEntry } from '@/components/ui/Menu'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Chip, EmptyState, Field, PageHeader, Textarea } from '@/components/ui/primitives'
import { APP_NAMES_REQUEST, useActivity } from '@/lib/activity/ActivityContext'
import { ActivityEventType } from '@/lib/activity/events'
import { appsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { SortType, type ListRequest } from '@/lib/api/list'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import { AppWorkspace } from '@/features/common/AppWorkspace'
import { storeFromSlug, storeSlug } from '@/features/common/store'
import { useLiveRefresh } from '@/features/notifications/live'
import { reviewKeys, reviewsApi } from './api'
import { useReviewsText, type ReviewsDictionary } from './i18n'
import { fillVariables, graphemeCount, repliesFor, variantFor } from './text'
import { ReviewReplyState, type ReviewFilter, type ReviewListDto, type SavedReplyDto } from './types'

// Design 9.1-9.8: one inbox for every app's App Store and Google Play reviews. Filters live in the address (shared views),
// the list is on the left and the selected review (?id=) on the right with its reply area. A reply is a store job.

const ALL = 'all'
const LIVE_TYPES = [ActivityEventType.ReviewReceived]
const DEFAULT_PAGE_SIZE = 20
const RATINGS = ['1-2', '3', '4-5', '1', '5'] as const

/** The address filters as the API's query; `fixedAppId` is the app of a per app inbox. */
export function reviewFilter(params: URLSearchParams, fixedAppId: number | null): ReviewFilter {
  const app = Number(params.get('app'))
  const [min, max] = (params.get('rating') ?? '').split('-').map(Number)
  const replied = params.get('replied')
  const read = params.get('read')
  return {
    AppId: fixedAppId ?? (Number.isInteger(app) && app > 0 ? app : null),
    Store: storeFromSlug(params.get('store')),
    MinRating: min > 0 ? min : null,
    MaxRating: max > 0 ? max : min > 0 ? min : null,
    Locale: params.get('lang') || null,
    HasReply: replied === 'yes' ? true : replied === 'no' ? false : null,
    IsRead: read === 'no' ? false : null,
    Search: params.get('q') || null,
  }
}

function pageRequest(params: URLSearchParams): ListRequest {
  const page = Number(params.get('page'))
  const size = Number(params.get('size'))
  return {
    PageNumber: Number.isInteger(page) && page > 0 ? page : 1,
    RowCount: Number.isInteger(size) && size > 0 ? size : DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: 'ReviewDate', Type: SortType.Desc },
  }
}

function replyStateView(state: ReviewReplyState, t: ReviewsDictionary): { label: string; tone: BadgeTone } | null {
  switch (state) {
    case ReviewReplyState.Sending:
      return { label: t.state.sending, tone: 'info' }
    case ReviewReplyState.Published:
      return { label: t.state.published, tone: 'success' }
    case ReviewReplyState.Failed:
      return { label: t.state.failed, tone: 'danger' }
    default:
      return null
  }
}

function ReplyStateBadge({ state }: { state: ReviewReplyState }) {
  const t = useReviewsText()
  const view = replyStateView(state, t)
  return view ? (
    <Badge tone={view.tone} dot small>
      {view.label}
    </Badge>
  ) : null
}

function Stars({ rating }: { rating: number }) {
  const t = useReviewsText()
  return (
    <span className="inline-flex items-center gap-0.5 text-warningText" role="img" aria-label={interpolate(t.rating, { rating })}>
      {[1, 2, 3, 4, 5].map((value) => (
        <Star key={value} className={cn('h-3.5 w-3.5', value <= rating ? 'fill-current' : 'opacity-30')} aria-hidden />
      ))}
    </span>
  )
}

function useApps(organizationId: number) {
  return useQuery({
    queryKey: queryKeys.apps.list(organizationId, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organizationId, APP_NAMES_REQUEST),
    staleTime: 60_000,
  })
}

// ---- Reply area -------------------------------------------------------------------------------------------------------

function SavedReplyPicker({ review, onPick }: { review: ReviewListDto; onPick: (text: string, savedReplyId: number) => void }) {
  const t = useReviewsText()
  const organization = useCurrentOrganization()
  const replies = useQuery({
    queryKey: reviewKeys.savedReplies(organization.Id, null, null),
    queryFn: () => reviewsApi.savedReplies(organization.Id),
    staleTime: 60_000,
  })
  const offered = repliesFor(replies.data ?? [], review)
  const entries: MenuEntry[] = [
    { key: 'heading', heading: t.reply.savedHeading },
    ...(offered.length === 0
      ? [{ key: 'none', content: <p className="px-3 py-2 text-[13px] text-fg3">{t.reply.savedNone}</p> }]
      : offered.map((reply: SavedReplyDto) => {
          const variant = variantFor(reply, review.Locale)
          return {
            key: String(reply.Id),
            label: (
              <span className="grid">
                <span className="font-medium">{reply.Name}</span>
                <span className="truncate text-xs text-fg3">
                  {variant?.Locale} · {variant?.Text}
                </span>
              </span>
            ),
            onSelect: () =>
              onPick(
                fillVariables(variant?.Text ?? '', { author: review.AuthorName, app: review.AppName, version: review.AppVersion }),
                reply.Id,
              ),
          }
        })),
    { key: 'sep', separator: true },
    {
      key: 'manage',
      content: (
        <Link className="lnk block px-3 py-2 text-[13px]" to="/reviews/saved-replies">
          {t.reply.savedManage}
        </Link>
      ),
    },
  ]
  return <Menu label={t.reply.saved} className="w-[340px]" trigger={<Button size="sm">{t.reply.saved}</Button>} entries={entries} />
}

function ReplyEditor({ review, onDone }: { review: ReviewListDto; onDone?: () => void }) {
  const t = useReviewsText()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const { watchJobs } = useActivity()
  const [body, setBody] = useState(review.DraftBody ?? review.ReplyBody ?? '')
  const [savedReplyId, setSavedReplyId] = useState<number | null>(null)
  const max = review.ReplyMaxLength || (review.Store === StoreKind.Google ? 350 : 5970)
  const length = graphemeCount(body)
  const over = length > max
  const empty = body.trim().length === 0

  const send = useMutation({
    mutationFn: () => reviewsApi.reply(organization.Id, review.Id, body.trim(), savedReplyId),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.reply.sent, 'success', interpolate(t.reply.job, { id: jobId }))
      void queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      onDone?.()
    },
  })
  const draft = useMutation({
    mutationFn: () => reviewsApi.saveDraft(organization.Id, review.Id, body),
    onSuccess: () => {
      showToast(t.reply.draftSaved, 'success')
      void queryClient.invalidateQueries({ queryKey: reviewKeys.all })
    },
  })

  const limitLabel = review.Store === StoreKind.Google ? t.reply.limitGoogle : t.reply.limitApple
  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <b className="mr-auto text-[13.5px]">{t.reply.title}</b>
        <SavedReplyPicker
          review={review}
          onPick={(text, id) => {
            setBody(text)
            setSavedReplyId(id)
          }}
        />
      </div>
      <Field
        label={limitLabel}
        counter={{ value: length, max }}
        error={over ? interpolate(t.reply.overLimit, { count: length - max }) : null}
        hint={t.reply.hint}
      >
        <Textarea rows={6} value={body} placeholder={t.reply.placeholder} onChange={(event) => setBody(event.target.value)} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        {onDone ? (
          <Button variant="ghost" onClick={onDone}>
            {t.reply.cancelEdit}
          </Button>
        ) : null}
        <Button loading={draft.isPending} disabled={empty} onClick={() => draft.mutate()}>
          {t.reply.saveDraft}
        </Button>
        <Button variant="primary" loading={send.isPending} disabled={empty || over} onClick={() => send.mutate()}>
          {t.reply.send}
        </Button>
      </div>
    </div>
  )
}

function ReviewDetail({ review }: { review: ReviewListDto }) {
  const t = useReviewsText()
  const base = useT()
  const format = useFormat()
  const { can } = useOrganization()
  const [editing, setEditing] = useState(false)
  const canReply = can(OrgPermission.ReplyReviews)
  const meta = [review.AppVersion, review.Device, review.Territory, format.dateTime(review.ReviewDate)].filter(Boolean).join(' · ')
  const hasReply = review.ReplyState !== ReviewReplyState.None && !!review.ReplyBody

  let reply = null
  if (review.Store === StoreKind.Microsoft) {
    reply = <Callout tone="info">{t.reply.microsoft}</Callout>
  } else if (!canReply) {
    reply = (
      <>
        {hasReply ? <PublishedReply review={review} /> : null}
        <Callout tone="neutral" title={t.reply.lockedTitle}>
          {t.reply.lockedBody}
        </Callout>
      </>
    )
  } else if (review.ReplyState === ReviewReplyState.Failed) {
    reply = (
      <>
        <Callout tone="danger" title={t.reply.failedTitle}>
          {review.ReplyError ? <code className="mono block text-xs">{review.ReplyError}</code> : null}
          {review.ReplyJobId ? (
            <Link className="lnk mt-1 inline-block text-[13px]" to={`/activity/jobs/${review.ReplyJobId}`}>
              {t.reply.openJob}
            </Link>
          ) : null}
        </Callout>
        <ReplyEditor key={`${review.Id}-failed`} review={review} />
      </>
    )
  } else if (hasReply && !editing) {
    reply = (
      <PublishedReply
        review={review}
        action={
          review.ReplyState === ReviewReplyState.Published ? (
            <Button size="sm" onClick={() => setEditing(true)}>
              {t.reply.edit}
            </Button>
          ) : null
        }
      />
    )
  } else {
    reply = <ReplyEditor key={review.Id} review={review} onDone={editing ? () => setEditing(false) : undefined} />
  }

  return (
    <article className="card grid gap-3 p-4" aria-label={interpolate(t.detailLabel, { author: review.AuthorName ?? '-' })}>
      <div className="flex flex-wrap items-center gap-2">
        <b className="text-[13.5px]">{review.AppName ?? '-'}</b>
        <StoreTag store={review.Store} small />
        {review.Locale ? <Badge small>{review.Locale}</Badge> : null}
        <span className="ml-auto text-xs text-fg3">{meta}</span>
      </div>
      <div className="grid gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <b>{review.AuthorName ?? base.common.all}</b>
          <Stars rating={review.Rating} />
        </div>
        {review.Title ? <h2 className="text-[15px] font-semibold">{review.Title}</h2> : null}
        <p className="whitespace-pre-wrap text-[13.5px] text-fg2">{review.Body}</p>
      </div>
      {reply}
    </article>
  )
}

function PublishedReply({ review, action }: { review: ReviewListDto; action?: ReactNode }) {
  const t = useReviewsText()
  const format = useFormat()
  return (
    <section className="grid gap-1.5 rounded-md border border-border p-3" aria-label={t.reply.current}>
      <div className="flex flex-wrap items-center gap-2">
        <b className="text-[13px]">{t.reply.current}</b>
        <ReplyStateBadge state={review.ReplyState} />
        <span className="ml-auto">{action}</span>
      </div>
      <p className="whitespace-pre-wrap text-[13.5px]">{review.ReplyBody}</p>
      <span className="text-xs text-fg3">
        {[review.ReplyByUserName, review.ReplyDate ? format.dateTime(review.ReplyDate) : null].filter(Boolean).join(' · ')}
      </span>
    </section>
  )
}

// ---- Inbox ------------------------------------------------------------------------------------------------------------

function ReviewRow({ review, selected, onSelect }: { review: ReviewListDto; selected: boolean; onSelect: () => void }) {
  const format = useFormat()
  return (
    <li>
      <button
        type="button"
        className={cn('grid w-full gap-1 border-b border-border px-3 py-2.5 text-left hover:bg-bg2', selected && 'bg-bg2')}
        aria-current={selected ? 'true' : undefined}
        onClick={onSelect}
      >
        <span className="flex items-center gap-2">
          {!review.IsRead ? <span className="h-2 w-2 flex-none rounded-full bg-accent" aria-hidden /> : null}
          <b className={cn('min-w-0 flex-1 truncate text-[13px]', review.IsRead && 'font-medium')}>{review.AuthorName ?? '-'}</b>
          <span className="text-xs text-fg3">{format.relative(review.ReviewDate)}</span>
        </span>
        <span className="line-clamp-2 text-[13px] text-fg2">{review.Title ? `${review.Title}. ${review.Body ?? ''}` : review.Body}</span>
        <span className="flex flex-wrap items-center gap-1.5">
          <Stars rating={review.Rating} />
          <span className="text-xs">{review.AppName}</span>
          <StoreTag store={review.Store} small codeOnly />
          {review.Locale ? <span className="text-xs text-fg3">{review.Locale}</span> : null}
          <ReplyStateBadge state={review.ReplyState} />
        </span>
      </button>
    </li>
  )
}

function ReviewInbox({ fixedAppId }: { fixedAppId: number | null }) {
  const t = useReviewsText()
  const base = useT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const { watchJobs } = useActivity()
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const filter = useMemo(() => reviewFilter(params, fixedAppId), [params, fixedAppId])
  const request = useMemo(() => pageRequest(params), [params])
  const [search, setSearch] = useState(params.get('q') ?? '')
  const apps = useApps(organization.Id)
  useLiveRefresh(LIVE_TYPES, reviewKeys.all)

  const reviews = useQuery({
    queryKey: reviewKeys.list(organization.Id, filter, request),
    queryFn: () => reviewsApi.list(organization.Id, filter, request),
    placeholderData: keepPreviousData,
  })
  const markRead = useMutation({
    mutationFn: (ids: number[]) => reviewsApi.markRead(organization.Id, ids, true),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: reviewKeys.all }),
  })
  const sync = useMutation({
    mutationFn: () => reviewsApi.sync(organization.Id, fixedAppId),
    onSuccess: (jobIds) => {
      watchJobs(jobIds)
      showToast(interpolate(t.syncStarted, { count: jobIds.length }), 'success')
    },
    meta: { silentError: true },
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })

  const update = (changes: Record<string, string | null>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '' || value === ALL) {
        next.delete(key)
      } else {
        next.set(key, value)
      }
    }
    if (resetPage) {
      next.delete('page')
    }
    setParams(next, { replace: true })
  }

  const rows = reviews.data?.Data ?? []
  const selectedId = Number(params.get('id'))
  const selected = rows.find((row) => row.Id === selectedId) ?? null
  const filtered = ['app', 'store', 'rating', 'lang', 'replied', 'read', 'q'].some((key) => params.get(key))
  const locales = [...new Set([...rows.map((row) => row.Locale).filter((value): value is string => !!value), params.get('lang')])]
    .filter((value): value is string => !!value)
    .sort()

  const select = (review: ReviewListDto) => {
    update({ id: String(review.Id) }, false)
    if (!review.IsRead) {
      markRead.mutate([review.Id])
    }
  }

  const unread = rows.filter((row) => !row.IsRead).length

  return (
    <>
      <PageHeader
        title={t.title}
        subtitle={reviews.data ? interpolate(t.subtitle, { total: reviews.data.TotalDataCount ?? rows.length, unread }) : t.subtitleEmpty}
        actions={
          <>
            <Link className="btn" to="/reviews/saved-replies">
              {t.savedReplies}
            </Link>
            {can(OrgPermission.ReplyReviews) ? (
              <Button loading={sync.isPending} onClick={() => sync.mutate()}>
                <RefreshCw aria-hidden />
                {t.sync}
              </Button>
            ) : null}
          </>
        }
      />
      <div className="toolbar mb-3">
        <div className="toolbar" role="group" aria-label={t.filters.replied}>
          <Chip selected={params.get('replied') === 'no'} onClick={() => update({ replied: params.get('replied') === 'no' ? null : 'no' })}>
            {t.filters.unanswered}
          </Chip>
          <Chip selected={params.get('read') === 'no'} onClick={() => update({ read: params.get('read') === 'no' ? null : 'no' })}>
            {t.filters.unread}
          </Chip>
        </div>
        <form
          className="w-full sm:w-60"
          onSubmit={(event) => {
            event.preventDefault()
            update({ q: search.trim() || null })
          }}
        >
          <input
            className="inp"
            type="search"
            aria-label={t.filters.search}
            placeholder={t.filters.search}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </form>
        {fixedAppId === null ? (
          <span className="w-[calc(50%-4px)] sm:w-44">
            <Select
              aria-label={t.filters.app}
              searchable
              value={params.get('app') ?? ALL}
              options={[
                { value: ALL, label: interpolate(t.filters.appAll, { all: base.common.all }) },
                ...(apps.data?.Data ?? []).map((app) => ({ value: String(app.Id), label: app.Name })),
              ]}
              onChange={(value) => update({ app: value })}
            />
          </span>
        ) : null}
        <span className="w-[calc(50%-4px)] sm:w-40">
          <Select
            aria-label={t.filters.store}
            value={params.get('store') ?? ALL}
            options={[
              { value: ALL, label: interpolate(t.filters.storeAll, { all: base.common.all }) },
              { value: storeSlug(StoreKind.Apple), label: base.enums.store.apple },
              { value: storeSlug(StoreKind.Google), label: base.enums.store.google },
            ]}
            onChange={(value) => update({ store: value })}
          />
        </span>
        <span className="w-[calc(50%-4px)] sm:w-36">
          <Select
            aria-label={t.filters.rating}
            value={params.get('rating') ?? ALL}
            options={[
              { value: ALL, label: interpolate(t.filters.ratingAll, { all: base.common.all }) },
              ...RATINGS.map((value) => ({ value, label: interpolate(t.filters.ratingValue, { value }) })),
            ]}
            onChange={(value) => update({ rating: value })}
          />
        </span>
        <span className="w-[calc(50%-4px)] sm:w-36">
          <Select
            aria-label={t.filters.language}
            value={params.get('lang') ?? ALL}
            options={[
              { value: ALL, label: interpolate(t.filters.languageAll, { all: base.common.all }) },
              ...locales.map((locale) => ({ value: locale, label: locale })),
            ]}
            onChange={(value) => update({ lang: value })}
          />
        </span>
        {filtered ? (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setSearch('')
              update({ app: null, store: null, rating: null, lang: null, replied: null, read: null, q: null })
            }}
          >
            {base.common.clearFilters}
          </Button>
        ) : null}
      </div>
      <Callout tone="info" className="mb-3">
        {t.microsoftNote}
      </Callout>
      {reviews.isLoading ? (
        <CardSkeleton />
      ) : reviews.isError ? (
        <ScreenState error={reviews.error} onRetry={() => void reviews.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          icon={<MessageSquare aria-hidden />}
          title={params.get('replied') === 'no' ? t.empty.unansweredTitle : filtered ? t.empty.filteredTitle : t.empty.title}
          description={filtered ? t.empty.filteredBody : t.empty.body}
          action={
            filtered ? (
              <Button
                onClick={() => {
                  setSearch('')
                  update({ app: null, store: null, rating: null, lang: null, replied: null, read: null, q: null })
                }}
              >
                {base.common.clearFilters}
              </Button>
            ) : null
          }
        />
      ) : (
        <div className="grid gap-3 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
          <div className="card relative overflow-hidden" aria-busy={reviews.isFetching || undefined}>
            {reviews.isFetching ? <div className="topline" aria-hidden /> : null}
            <ul aria-label={t.listLabel}>
              {rows.map((review) => (
                <ReviewRow key={review.Id} review={review} selected={review.Id === selected?.Id} onSelect={() => select(review)} />
              ))}
            </ul>
            <Pagination
              page={request.PageNumber}
              pageCount={reviews.data?.TotalPageCount ?? 1}
              total={reviews.data?.TotalDataCount ?? rows.length}
              pageSize={request.RowCount}
              onPage={(page) => update({ page: String(page) }, false)}
              onPageSize={(size) => update({ size: String(size) })}
            />
          </div>
          {selected ? (
            <ReviewDetail key={selected.Id} review={selected} />
          ) : (
            <div className="card grid place-items-center p-8 text-center text-[13px] text-fg3">
              {t.pick}
            </div>
          )}
        </div>
      )}
    </>
  )
}

/** /reviews: every app of the organization. */
export function ReviewsPage() {
  return <ReviewInbox fixedAppId={null} />
}

/** /apps/:appId/reviews: the same inbox, fixed to one app. */
export function AppReviewsPage() {
  return (
    <AppWorkspace section="reviews" appWide>
      {(context) => <ReviewInbox fixedAppId={context.app.Id} />}
    </AppWorkspace>
  )
}
