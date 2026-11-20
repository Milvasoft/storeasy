import { useEffect, useMemo, useRef, useState, type DragEvent, type HTMLAttributes, type ReactNode } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowLeft, EyeOff, LayoutGrid, Plus, RefreshCw, SearchX, Table2 } from 'lucide-react'
import { AddGoogleAppDialog } from '@/components/apps/AppDialogs'
import { AppAvatar } from '@/components/apps/AppAvatar'
import { AppOrderHandle, type OrderFocus } from '@/components/apps/AppOrderHandle'
import { HideAppButton } from '@/components/apps/HideAppButton'
import { MutedAlertsIcon } from '@/components/apps/MutedAlertsIcon'
import { inOrder, useReorderApps } from '@/components/apps/useReorderApps'
import { Badge, StoreTag, storeName } from '@/components/ui/Badge'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { ScreenState } from '@/components/ui/ScreenState'
import { SegmentedControl } from '@/components/ui/Tabs'
import { Button, CardSkeleton, Chip, EmptyState, PageHeader, TableSkeleton } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { isActive } from '@/lib/activity/hatState'
import { appsApi, connectionsApi, type AppListRequest } from '@/lib/api/endpoints'
import { FilterType, type ListRequest } from '@/lib/api/list'
import { OrgPermission, StoreKind, type AppListDto } from '@/lib/api/types'
import { lastSyncOf, moveBy, moveTo, primaryIdentifier, recordFor, recordsOf, STORE_ORDER, storeCounts } from '@/lib/apps/apps'
import { useSyncApps } from '@/lib/apps/useSyncApps'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { AddMsProductDialog } from '@/features/microsoft/AddMsProductDialog'
import { useMs } from '@/features/microsoft/i18n/useMs'

const DEFAULT_PAGE_SIZE = 20
const SEARCH_DELAY_MS = 400
const STORE_PARAM: Record<string, StoreKind> = { apple: StoreKind.Apple, google: StoreKind.Google, microsoft: StoreKind.Microsoft }
const PARAM_OF: Record<StoreKind, string> = { [StoreKind.Apple]: 'apple', [StoreKind.Google]: 'google', [StoreKind.Microsoft]: 'microsoft' }
// One row is enough: the hidden apps link needs only the total.
const HIDDEN_COUNT_REQUEST: AppListRequest = { PageNumber: 1, RowCount: 1, Hidden: true }

function numberParam(value: string | null): number | null {
  const parsed = Number(value)
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null
}

/**
 * The apps request from the address: page, size and the name search (Contains). No sort is sent, so the server answers
 * in the order members chose (then by name for the apps never placed).
 */
export function appsRequest(params: URLSearchParams): ListRequest {
  const search = params.get('q')?.trim()
  return {
    PageNumber: numberParam(params.get('page')) ?? 1,
    RowCount: numberParam(params.get('size')) ?? DEFAULT_PAGE_SIZE,
    ...(search ? { Filtering: { Criterias: [{ FilterBy: 'Name', Value: search, Type: FilterType.Contains }] } } : {}),
  }
}

function useRunningApps(): Set<number> {
  const { state } = useActivity()
  return useMemo(
    () =>
      new Set(
        Object.values(state.jobs)
          .filter((job) => isActive(job) && job.appId !== null)
          .map((job) => job.appId as number),
      ),
    [state.jobs],
  )
}

function SyncBadge({ synced }: { synced: boolean }) {
  const t = useT()
  return synced ? (
    <Badge tone="success" small>
      {t.apps.synced}
    </Badge>
  ) : (
    <Badge tone="info" small dot>
      {t.apps.awaitingSync}
    </Badge>
  )
}

/** One app as a card (design 6.1): icon and identifier, one lane per store record, the date it was added. */
function AppCard({
  app,
  running,
  action,
  dragging,
  cardProps,
}: {
  app: AppListDto
  running: boolean
  action?: ReactNode
  dragging?: boolean
  cardProps?: HTMLAttributes<HTMLElement>
}) {
  const t = useT()
  const format = useFormat()
  const identifier = primaryIdentifier(app)
  return (
    <article className={dragging ? 'appc dragging' : 'appc'} {...cardProps}>
      <div className="flex min-w-0 items-center gap-3">
        <AppAvatar app={app} size="m" />
        <div className="min-w-0">
          <h3 className="h3 flex min-w-0 items-center gap-1.5">
            <Link className="truncate hover:underline" to={`/apps/${app.Id}`}>
              {app.Name}
            </Link>
            <MutedAlertsIcon app={app} />
          </h3>
          {identifier ? <div className="mono truncate text-xs text-fg3">{identifier}</div> : null}
        </div>
        {running || action ? (
          <span className="ml-auto flex shrink-0 items-center gap-1">
            {running ? (
              <Badge tone="accent" small live>
                {t.jobs.state.running}
              </Badge>
            ) : null}
            {action}
          </span>
        ) : null}
      </div>
      <div className="lanes">
        {recordsOf(app).map((record) => (
          <div key={record.StoreAppId} className="lane">
            <StoreTag store={record.Store} connectionId={record.StoreConnectionId} small codeOnly />
            <span className="v truncate">
              {record.Name ?? app.Name} <small className="mono">· {record.ExternalId ?? record.BundleId}</small>
            </span>
            <SyncBadge synced={!!record.SyncedDate} />
          </div>
        ))}
      </div>
      <div className="foot">
        <span>{interpolate(t.app.addedOn, { date: format.date(app.CreationDate) })}</span>
        <span>
          {STORE_ORDER.filter((store) => recordFor(app, store))
            .map((store) => storeName(store, t.enums.store))
            .join(' · ')}
        </span>
      </div>
    </article>
  )
}

function StoreCell({ app, store }: { app: AppListDto; store: StoreKind }) {
  const record = recordFor(app, store)
  if (!record) {
    return <span className="text-fg3">-</span>
  }
  return (
    <span className="grid min-w-0 justify-items-start gap-1">
      <span className="mono max-w-full truncate text-[12.5px]">{record.ExternalId ?? record.BundleId ?? '-'}</span>
      <SyncBadge synced={!!record.SyncedDate} />
    </span>
  )
}

/**
 * The apps (design 6.1-6.6): cards or a table (?view=table), a name search in the address, store chips, the last sync
 * and "Pull from stores". With no store connection yet the page leads to the connections (6.4). Hidden apps are left
 * out; ?hidden=1 lists only them, each with "Show" to bring it back.
 */
export function AppsPage() {
  const t = useT()
  const tm = useMs()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const [params, setParams] = useSearchParams()
  const showHidden = params.get('hidden') === '1'
  const request = useMemo<AppListRequest>(() => ({ ...appsRequest(params), ...(showHidden ? { Hidden: true } : {}) }), [params, showHidden])
  const view = params.get('view') === 'table' ? 'table' : 'grid'
  const storeFilter = STORE_PARAM[params.get('store') ?? ''] ?? null
  const [search, setSearch] = useState(params.get('q') ?? '')
  const [adding, setAdding] = useState(false)
  const [addingMs, setAddingMs] = useState(false)
  const running = useRunningApps()
  const sync = useSyncApps()
  const editor = can(OrgPermission.EditContent)

  const apps = useQuery({
    // The store chip filters on the server (GetAppListQuery.Store), so paging counts the filtered apps.
    queryKey: [...queryKeys.apps.list(organization.Id, request), storeFilter],
    queryFn: () => appsApi.list(organization.Id, request, storeFilter),
    placeholderData: keepPreviousData,
  })
  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
  })
  const hiddenApps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, HIDDEN_COUNT_REQUEST),
    queryFn: () => appsApi.list(organization.Id, HIDDEN_COUNT_REQUEST),
  })
  const hiddenCount = hiddenApps.data?.TotalDataCount ?? 0

  const update = (changes: Record<string, string | null>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') {
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

  // The search is sent 400 ms after typing stops and starts again from page 1 (design 6.2).
  useEffect(() => {
    if (search.trim() === (params.get('q') ?? '')) {
      return
    }
    const timer = window.setTimeout(() => update({ q: search.trim() || null }), SEARCH_DELAY_MS)
    return () => window.clearTimeout(timer)
    // update reads the latest params when it runs; re-arming on every params change would restart the delay.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  const all = apps.data?.Data ?? []
  const rows = all
  // Per store counts are known only for the unfiltered list; a filtered page says how many matched the chosen store.
  const counts = storeCounts(all)
  const lastSync = lastSyncOf(all)
  const searching = !!params.get('q')
  const noConnections = connections.isSuccess && connections.data.length === 0

  // Reordering (cards only): the whole shown list must be on the page, so not while searching, filtering by store, in
  // the hidden apps or with more than one page.
  const reorder = useReorderApps([...queryKeys.apps.list(organization.Id, request), storeFilter])
  const listed = editor && !showHidden && view === 'grid' && all.length > 1
  const paged = (apps.data?.TotalDataCount ?? all.length) > all.length
  const canReorder = listed && !searching && !storeFilter && !paged
  const reorderNote = !listed
    ? null
    : searching || storeFilter
      ? t.apps.reorderOffFiltered
      : paged
        ? t.apps.reorderOffPaged
        : t.apps.reorderHint
  const [dragging, setDragging] = useState<number | null>(null)
  const [preview, setPreview] = useState<number[] | null>(null)
  const [announcement, setAnnouncement] = useState('')
  const dropped = useRef(false)
  const cards = useRef(new Map<number, HTMLElement>())
  const ids = all.map((app) => app.Id)
  const shown = preview ? inOrder(all, preview) : all

  // A new read (the saved order, or the old one back after a refusal) replaces the order shown while dragging.
  useEffect(() => {
    if (dragging === null) {
      setPreview(null)
    }
    // Only a new answer resets the preview; the end of a drag clears it itself.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apps.data])

  const save = (next: number[]) => {
    if (next.some((id, index) => id !== ids[index])) {
      reorder.mutate(next)
      return true
    }
    return false
  }

  const announce = (app: AppListDto, next: number[]) =>
    setAnnouncement(interpolate(t.apps.movedTo, { app: app.Name, position: next.indexOf(app.Id) + 1 }))

  const move = (app: AppListDto, offset: -1 | 1, focus: OrderFocus) => {
    const next = moveBy(ids, app.Id, offset)
    if (!save(next)) {
      return
    }
    announce(app, next)
    // The card moved in the page: put the focus back on the control that moved it (the handle at either end).
    window.requestAnimationFrame(() => {
      const card = cards.current.get(app.Id)
      const target = card?.querySelector<HTMLButtonElement>(`[data-reorder="${focus}"]`)
      const fallback = card?.querySelector<HTMLButtonElement>('[data-reorder="handle"]')
      ;(target && !target.disabled ? target : fallback)?.focus()
    })
  }

  const dragProps = (app: AppListDto): HTMLAttributes<HTMLElement> => ({
    draggable: true,
    onDragStart: (event: DragEvent<HTMLElement>) => {
      dropped.current = false
      if (event.dataTransfer) {
        event.dataTransfer.setData('text/plain', String(app.Id))
        event.dataTransfer.effectAllowed = 'move'
      }
      setDragging(app.Id)
      setPreview(ids)
    },
    onDragOver: (event: DragEvent<HTMLElement>) => {
      if (dragging === null) {
        return
      }
      event.preventDefault()
      if (app.Id !== dragging) {
        setPreview((current) => moveTo(current ?? ids, dragging, app.Id))
      }
    },
    onDragEnd: () => {
      if (!dropped.current) {
        setPreview(null)
      }
      setDragging(null)
    },
  })

  const onListDragOver = (event: DragEvent<HTMLElement>) => {
    if (dragging !== null) {
      event.preventDefault()
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = 'move'
      }
    }
  }

  const onListDrop = (event: DragEvent<HTMLElement>) => {
    if (dragging === null) {
      return
    }
    event.preventDefault()
    dropped.current = true
    const next = preview ?? ids
    const app = all.find((item) => item.Id === dragging)
    setDragging(null)
    if (save(next) && app) {
      announce(app, next)
    } else {
      setPreview(null)
    }
  }

  const columns: Column<AppListDto>[] = [
    {
      key: 'app',
      header: t.apps.app,
      role: 'main',
      cell: (app) => (
        <span className="flex min-w-0 items-center gap-2.5">
          <AppAvatar app={app} size="s" />
          <span className="grid min-w-0">
            <span className="flex min-w-0 items-center gap-1.5">
              <b className="truncate">{app.Name}</b>
              <MutedAlertsIcon app={app} />
            </span>
            <span className="mono truncate text-[11.5px] text-fg3">{primaryIdentifier(app) ?? ''}</span>
          </span>
        </span>
      ),
    },
    ...STORE_ORDER.map((store) => ({
      key: `store-${store}`,
      header: storeName(store, t.enums.store),
      cell: (app: AppListDto) => <StoreCell app={app} store={store} />,
    })),
    {
      key: 'added',
      header: t.apps.added,
      role: 'hidePhone' as const,
      cell: (app: AppListDto) => <span className="tabular-nums">{format.date(app.CreationDate)}</span>,
    },
    ...(editor
      ? [{ key: 'actions', header: t.apps.actions, role: 'action' as const, cell: (app: AppListDto) => <HideAppButton app={app} /> }]
      : []),
  ]

  const header = (
    <PageHeader
      title={showHidden ? t.apps.hiddenTitle : t.apps.title}
      subtitle={showHidden ? t.apps.hiddenSubtitle : t.apps.subtitle}
      actions={
        <>
          {showHidden ? (
            <Button variant="ghost" onClick={() => update({ hidden: null })}>
              <ArrowLeft aria-hidden />
              {t.apps.backToList}
            </Button>
          ) : hiddenCount > 0 ? (
            <Button variant="ghost" onClick={() => update({ hidden: '1' })}>
              <EyeOff aria-hidden />
              {interpolate(t.apps.hiddenLink, { count: hiddenCount })}
            </Button>
          ) : null}
          <SegmentedControl
            label={t.apps.view}
            value={view}
            onChange={(value) => update({ view: value === 'table' ? 'table' : null }, false)}
            options={[
              {
                value: 'grid',
                label: (
                  <span className="inline-flex items-center gap-1.5">
                    <LayoutGrid className="h-3.5 w-3.5" aria-hidden />
                    <span className="sr-only sm:not-sr-only">{t.apps.gridView}</span>
                  </span>
                ),
              },
              {
                value: 'table',
                label: (
                  <span className="inline-flex items-center gap-1.5">
                    <Table2 className="h-3.5 w-3.5" aria-hidden />
                    <span className="sr-only sm:not-sr-only">{t.apps.tableView}</span>
                  </span>
                ),
              },
            ]}
          />
          {editor && !showHidden ? (
            <>
              <Button onClick={() => setAdding(true)}>
                <Plus aria-hidden />
                {t.apps.addGoogle}
              </Button>
              <Button onClick={() => setAddingMs(true)}>
                <Plus aria-hidden />
                {tm.addProduct.open}
              </Button>
              <Button variant="primary" loading={sync.isPending} disabled={noConnections} onClick={() => sync.mutate(null)}>
                <RefreshCw aria-hidden />
                {t.apps.sync}
              </Button>
            </>
          ) : null}
        </>
      }
    />
  )

  let body
  if (apps.isLoading) {
    body =
      view === 'table' ? (
        <TableSkeleton columns={[t.apps.app, t.enums.store.apple, t.enums.store.google]} label={t.common.loading} />
      ) : (
        <CardSkeleton count={6} />
      )
  } else if (apps.isError) {
    body = <ScreenState error={apps.error} onRetry={() => void apps.refetch()} />
  } else if (all.length === 0 && searching) {
    body = (
      <EmptyState
        icon={<SearchX aria-hidden />}
        title={interpolate(t.common.noResultsFor, { term: params.get('q') ?? '' })}
        description={t.apps.noResultsBody}
        action={
          <Button
            onClick={() => {
              setSearch('')
              update({ q: null, store: null })
            }}
          >
            {t.common.clearSearch}
          </Button>
        }
      />
    )
  } else if (all.length === 0 && storeFilter) {
    body = (
      <EmptyState
        icon={<SearchX aria-hidden />}
        title={interpolate(t.common.noResultsFor, { term: storeName(storeFilter, t.enums.store) })}
        action={<Button onClick={() => update({ store: null })}>{t.activity.allStores}</Button>}
      />
    )
  } else if (all.length === 0 && showHidden) {
    body = (
      <EmptyState
        icon={<EyeOff aria-hidden />}
        title={t.apps.hiddenEmptyTitle}
        description={t.apps.hiddenEmptyBody}
        action={<Button onClick={() => update({ hidden: null })}>{t.apps.backToList}</Button>}
      />
    )
  } else if (all.length === 0 && noConnections) {
    // Design 6.4: the first open of a new organization leads to the store connections, or says who can add one.
    body = (
      <EmptyState
        icon={<LayoutGrid aria-hidden />}
        title={t.sections.apps.emptyTitle}
        description={t.sections.apps.emptyBody}
        action={
          can(OrgPermission.ManageConnections) ? (
            <Link className="btn pri" to="/connections">
              {t.sections.apps.action}
            </Link>
          ) : (
            <span className="text-[13px] text-fg3">{t.sections.apps.askAdmin}</span>
          )
        }
      >
        <p className="!mt-2 text-xs text-fg3">{t.apps.keysNote}</p>
      </EmptyState>
    )
  } else if (all.length === 0) {
    body = (
      <EmptyState
        icon={<LayoutGrid aria-hidden />}
        title={t.apps.noAppsTitle}
        description={t.apps.noAppsBody}
        action={
          editor ? (
            <>
              <Button variant="primary" loading={sync.isPending} onClick={() => sync.mutate(null)}>
                {t.apps.sync}
              </Button>
              <Button onClick={() => setAdding(true)}>{t.apps.addGoogle}</Button>
              <Button onClick={() => setAddingMs(true)}>{tm.addProduct.open}</Button>
            </>
          ) : undefined
        }
      />
    )
  } else {
    body = (
      <>
        {view === 'table' ? (
          <DataTable
            caption={showHidden ? t.apps.hiddenListLabel : t.apps.listLabel}
            columns={columns}
            rows={rows}
            rowKey={(app) => app.Id}
            busy={apps.isFetching}
            onRowClick={(app) => navigate(`/apps/${app.Id}`)}
          />
        ) : (
          <div
            className="applist"
            aria-busy={apps.isFetching || undefined}
            onDragOver={canReorder ? onListDragOver : undefined}
            onDrop={canReorder ? onListDrop : undefined}
          >
            {shown.map((app, index) => (
              <div
                key={app.Id}
                className="contents"
                ref={(element) => {
                  if (element) {
                    cards.current.set(app.Id, element)
                  } else {
                    cards.current.delete(app.Id)
                  }
                }}
              >
                <AppCard
                  app={app}
                  running={running.has(app.Id)}
                  dragging={dragging === app.Id}
                  cardProps={canReorder ? dragProps(app) : undefined}
                  action={
                    editor ? (
                      <>
                        {canReorder ? (
                          <AppOrderHandle
                            app={app}
                            first={index === 0}
                            last={index === shown.length - 1}
                            onMove={(offset, focus) => move(app, offset, focus)}
                          />
                        ) : null}
                        <HideAppButton app={app} />
                      </>
                    ) : undefined
                  }
                />
              </div>
            ))}
          </div>
        )}
        <Pagination
          page={apps.data?.CurrentPageNumber ?? request.PageNumber}
          pageCount={apps.data?.TotalPageCount ?? 1}
          total={apps.data?.TotalDataCount ?? all.length}
          pageSize={request.RowCount}
          loadingNext={apps.isFetching && apps.isPlaceholderData}
          onPage={(page) => update({ page: String(page) }, false)}
          onPageSize={(size) => update({ size: String(size) })}
        />
      </>
    )
  }

  return (
    <>
      {header}
      <div className="toolbar">
        <span className="w-full sm:w-72">
          <input
            className="inp"
            type="search"
            aria-label={t.apps.search}
            placeholder={t.apps.search}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </span>
        <span role="group" aria-label={t.activity.storeFilter} className="flex flex-wrap gap-2">
          <Chip selected={!storeFilter} onClick={() => update({ store: null })}>
            {t.activity.allStores}
          </Chip>
          {STORE_ORDER.map((store) => (
            <Chip key={store} selected={storeFilter === store} onClick={() => update({ store: PARAM_OF[store] })}>
              <StoreTag store={store} small codeOnly />
              {storeFilter && storeFilter !== store
                ? storeName(store, t.enums.store)
                : interpolate(t.apps.storeChip, {
                    store: storeName(store, t.enums.store),
                    count: storeFilter ? (apps.data?.TotalDataCount ?? counts[store]) : counts[store],
                  })}
            </Chip>
          ))}
        </span>
        <span className="ml-auto text-xs text-fg3">
          {lastSync ? interpolate(t.apps.lastSync, { time: format.relative(lastSync) }) : all.length > 0 ? t.apps.neverSynced : null}
        </span>
      </div>
      {reorderNote && !apps.isLoading && !apps.isError ? <p className="mb-3 mt-0 text-xs text-fg3">{reorderNote}</p> : null}
      <p className="sr-only" role="status" aria-live="polite">
        {announcement}
      </p>
      {body}
      {editor ? (
        <>
          <AddGoogleAppDialog open={adding} onClose={() => setAdding(false)} onAdded={(appId) => navigate(`/apps/${appId}`)} />
          <AddMsProductDialog open={addingMs} onClose={() => setAddingMs(false)} onAdded={(appId) => navigate(`/apps/${appId}`)} />
        </>
      ) : null}
    </>
  )
}
