import type { ReactNode } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Eye } from 'lucide-react'
import { AppAvatar } from '@/components/apps/AppAvatar'
import { StoreTag, storeName } from '@/components/ui/Badge'
import { Banner } from '@/components/ui/Feedback'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { PageSkeleton } from '@/components/ui/primitives'
import { appsApi } from '@/lib/api/endpoints'
import { OrgPermission, StoreKind, type AppDetailDto, type AppStoreRecordDto } from '@/lib/api/types'
import { recordsOf } from '@/lib/apps/apps'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { cn } from '@/lib/utils/cn'
import { useP2 } from './i18n/useP2'
import type { P2Dictionary } from './i18n/en'
import { storesWith } from '@/lib/stores/channels'
import { storeFromSlug, storeSlug } from './store'

// The frame of the app screens of phase 2 (design 6.7: one store picker, the app's sections, the section's sub parts in a
// chip row). The overview, builds and jobs tabs belong to AppDetailPage; this frame links to them and they link here.

export type WorkspaceSection =
  | 'readiness'
  | 'preReview'
  | 'listing'
  | 'keywords'
  | 'experiments'
  | 'media'
  | 'release'
  | 'reviews'
  | 'storePerformance'
  | 'rejections'
  | 'money'
  | 'answers'
  | 'assisted'
  | 'compliance'
  | 'profile'
  | 'microsoft'
  | 'winget'

export type WorkspaceSub =
  | 'texts'
  | 'details'
  | 'category'
  | 'ageRating'
  | 'pricing'
  | 'screenshots'
  | 'previews'
  | 'versions'
  | 'testers'
  | 'reviewInfo'
  | 'subscriptions'
  | 'iap'
  | 'purchaseSettings'
  | 'testflight'
  | 'performance'
  | 'impact'

interface SubItem {
  key: WorkspaceSub
  path: string
  stores: StoreKind[]
}

const BOTH = [StoreKind.Apple, StoreKind.Google]
const APPLE = [StoreKind.Apple]
const GOOGLE = [StoreKind.Google]
// Store page performance needs store metrics (winget has none).
const ALL = storesWith('hasMetrics')

// What each sub part supports; a sub part is offered only for the stores its API covers.
const SUBS: Partial<Record<WorkspaceSection, SubItem[]>> = {
  listing: [
    { key: 'texts', path: 'listing/texts', stores: BOTH },
    { key: 'details', path: 'listing/details', stores: GOOGLE },
    { key: 'category', path: 'listing/category', stores: APPLE },
    { key: 'ageRating', path: 'listing/age-rating', stores: APPLE },
    { key: 'pricing', path: 'listing/pricing', stores: APPLE },
  ],
  media: [
    { key: 'screenshots', path: 'listing/screenshots', stores: BOTH },
    { key: 'previews', path: 'listing/previews', stores: APPLE },
  ],
  release: [
    { key: 'versions', path: 'release/versions', stores: BOTH },
    { key: 'testers', path: 'release/testers', stores: GOOGLE },
    { key: 'testflight', path: 'release/testflight', stores: APPLE },
    { key: 'reviewInfo', path: 'release/review-info', stores: APPLE },
  ],
  storePerformance: [
    { key: 'performance', path: 'store-performance', stores: ALL },
    { key: 'impact', path: 'store-performance/impact', stores: ALL },
  ],
  money: [
    { key: 'subscriptions', path: 'money/subscriptions', stores: BOTH },
    { key: 'iap', path: 'money/iap', stores: BOTH },
    { key: 'purchaseSettings', path: 'money/settings', stores: BOTH },
  ],
}

const SECTION_PATHS: Record<WorkspaceSection, string> = {
  readiness: 'readiness',
  preReview: 'pre-review',
  listing: 'listing/texts',
  keywords: 'keywords',
  media: 'listing/screenshots',
  experiments: 'experiments',
  release: 'release/versions',
  money: 'money/subscriptions',
  reviews: 'reviews',
  storePerformance: 'store-performance',
  rejections: 'rejections',
  answers: 'answers',
  assisted: 'assisted',
  compliance: 'compliance',
  profile: 'settings/profile',
  microsoft: 'ms/product',
  winget: 'winget',
}

// Sections that exist only for some stores: their tab shows when the app has a record there.
const SECTION_STORES: Partial<Record<WorkspaceSection, StoreKind[]>> = {
  microsoft: [StoreKind.Microsoft],
  winget: [StoreKind.Winget],
  storePerformance: storesWith('hasMetrics'),
  money: [StoreKind.Apple, StoreKind.Google],
  keywords: [StoreKind.Apple, StoreKind.Google],
}

function sectionShown(section: WorkspaceSection, stores: StoreKind[]): boolean {
  const only = SECTION_STORES[section]
  return !only || only.some((store) => stores.includes(store))
}

const SECTIONS: WorkspaceSection[] = [
  'readiness',
  'preReview',
  'listing',
  'keywords',
  'media',
  'experiments',
  'release',
  'money',
  'reviews',
  'storePerformance',
  'rejections',
  'answers',
  'assisted',
  'compliance',
  'profile',
  'microsoft',
  'winget',
]

function sectionLabel(section: WorkspaceSection, t: P2Dictionary): string {
  return t.workspace[section]
}

/** The phase 2 tabs, for the app detail's own tab row (route tabs; the store goes along when known). */
export function workspaceTabs(
  appId: number,
  t: P2Dictionary,
  stores: StoreKind[] = [],
): Array<{ value: string; label: string; to: string }> {
  return SECTIONS.filter((section) => sectionShown(section, stores)).map((section) => ({
    value: section,
    label: sectionLabel(section, t),
    to: `/apps/${appId}/${SECTION_PATHS[section]}`,
  }))
}

export interface WorkspaceContext {
  app: AppDetailDto
  organizationId: number
  // The selected store and its record; null for app wide sections (answers, compliance, profile, assisted).
  store: StoreKind
  record: AppStoreRecordDto
  editor: boolean
  can: (permission: OrgPermission) => boolean
  // The query string to keep on links within the app ("?store=apple").
  storeQuery: string
}

interface AppWorkspaceProps {
  section: WorkspaceSection
  sub?: WorkspaceSub
  // Stores this screen works for; default every store of the app.
  stores?: StoreKind[]
  // App wide screens show no store picker.
  appWide?: boolean
  children: (context: WorkspaceContext) => ReactNode
}

export function AppWorkspace({ section, sub, stores, appWide, children }: AppWorkspaceProps) {
  const base = useT()
  const t = useP2()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const { appId: rawId = '' } = useParams()
  const appId = Number(rawId)
  const [search] = useSearchParams()

  const app = useQuery({
    queryKey: queryKeys.apps.detail(organization.Id, appId),
    queryFn: () => appsApi.detail(organization.Id, appId),
    enabled: Number.isInteger(appId) && appId > 0,
  })

  if (app.isLoading || (!app.data && !app.isError)) {
    return <PageSkeleton />
  }
  if (app.isError || !app.data) {
    return (
      <ScreenState
        error={app.error}
        onRetry={() => void app.refetch()}
        back={
          <Link className="btn inv" to="/apps">
            {base.app.back}
          </Link>
        }
      />
    )
  }

  const data = app.data
  const records = recordsOf(data)
  const subItems = SUBS[section] ?? []
  const subItem = subItems.find((item) => item.key === sub)
  const supported = stores ?? subItem?.stores ?? records.map((record) => record.Store)
  const requested = storeFromSlug(search.get('store'))
  const record =
    records.find((item) => item.Store === requested) ?? records.find((item) => supported.includes(item.Store)) ?? records[0] ?? null
  const storeQuery = record ? `?store=${storeSlug(record.Store)}` : ''
  const editor = can(OrgPermission.EditContent)

  const tabs = [
    { key: 'overview', label: base.app.overview, to: `/apps/${data.Id}`, on: false },
    { key: 'builds', label: base.app.builds, to: `/apps/${data.Id}/builds`, on: false },
    { key: 'jobs', label: base.app.jobs, to: `/apps/${data.Id}/jobs`, on: false },
    ...SECTIONS.filter((item) =>
      sectionShown(
        item,
        records.map((record) => record.Store),
      ),
    ).map((item) => ({
      key: item,
      label: sectionLabel(item, t),
      to: `/apps/${data.Id}/${SECTION_PATHS[item]}${storeQuery}`,
      on: item === section,
    })),
  ]

  let body: ReactNode
  if (!record) {
    body = <StateView icon={<Eye aria-hidden />} title={interpolate(t.workspace.noRecord, { store: '-' })} />
  } else if (!appWide && !supported.includes(record.Store)) {
    body = (
      <StateView
        icon={<Eye aria-hidden />}
        title={interpolate(t.workspace.notForStore, { store: storeName(record.Store, base.enums.store) })}
      />
    )
  } else {
    body = children({ app: data, organizationId: organization.Id, store: record.Store, record, editor, can, storeQuery })
  }

  return (
    <>
      <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to={`/apps/${data.Id}`}>
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.workspace.back}
      </Link>
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <AppAvatar app={data} size="m" />
        <h1 className="pg-t min-w-0 flex-1 truncate">{data.Name}</h1>
        {!appWide && records.length > 0 ? (
          <div className="toolbar" role="group" aria-label={t.workspace.store}>
            {records.map((item) => (
              <Link
                key={item.StoreAppId}
                to={`?store=${storeSlug(item.Store)}`}
                className={cn('chip', item.StoreAppId === record?.StoreAppId && 'on')}
                aria-current={item.StoreAppId === record?.StoreAppId ? 'true' : undefined}
              >
                <StoreTag store={item.Store} connectionId={item.StoreConnectionId} small />
              </Link>
            ))}
          </div>
        ) : null}
      </div>
      <nav className="tabs" aria-label={t.workspace.sections}>
        {tabs.map((tab) => (
          <Link key={tab.key} to={tab.to} className={cn('tab', tab.on && 'on')} aria-current={tab.on ? 'page' : undefined}>
            {tab.label}
          </Link>
        ))}
      </nav>
      {subItems.length > 0 ? (
        <div className="toolbar my-3" role="group" aria-label={sectionLabel(section, t)}>
          {subItems
            .filter((item) => !record || item.stores.includes(record.Store))
            .map((item) => (
              <Link
                key={item.key}
                to={`/apps/${data.Id}/${item.path}${storeQuery}`}
                className={cn('chip', item.key === sub && 'on')}
                aria-current={item.key === sub ? 'page' : undefined}
              >
                {t.sub[item.key]}
              </Link>
            ))}
        </div>
      ) : (
        <div className="h-3" />
      )}
      {!editor ? (
        <div className="mb-3">
          <Banner tone="info">{t.workspace.readOnly}</Banner>
        </div>
      ) : null}
      {body}
    </>
  )
}
