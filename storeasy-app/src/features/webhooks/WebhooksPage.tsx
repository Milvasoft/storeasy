import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Pencil, Plus, RefreshCw, Send, ShieldAlert, Trash2, Webhook } from 'lucide-react'
import { Badge, StoreTag, type BadgeTone } from '@/components/ui/Badge'
import { SecretBox } from '@/components/ui/Code'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Tabs } from '@/components/ui/Tabs'
import {
  Button,
  CardSkeleton,
  Checkbox,
  EmptyState,
  Field,
  IconButton,
  Input,
  PageHeader,
  RadioGroup,
  Switch,
} from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { SortType, type ListRequest } from '@/lib/api/list'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { NotificationEntity } from '@/features/notifications/types'
import { webhookKeys, webhooksApi } from './api'
import { useWebhooksText, type WebhooksDictionary } from './i18n'
import {
  ORG_WEBHOOK_EVENTS,
  WebhookDeliveryStatus,
  WebhookProcessResult,
  WebhookRegistrationState,
  WebhookSource,
  type OrgWebhookDto,
  type WebhookDeliveryDto,
  type WebhookEndpointDto,
  type WebhookEventDto,
  type WebhookSecretDto,
} from './types'

// Design 14.15-14.18 and the outgoing subscriptions: inbound endpoints (one per Apple connection, or a signed generic
// one) with their event log, and the organization's own subscriptions with their delivery log. Secrets show once.

const DEFAULT_PAGE_SIZE = 20

type Tab = 'inbound' | 'outgoing'

function registrationView(state: WebhookRegistrationState, t: WebhooksDictionary): { label: string; tone: BadgeTone } {
  switch (state) {
    case WebhookRegistrationState.Api:
      return { label: t.registration.api, tone: 'success' }
    case WebhookRegistrationState.Manual:
      return { label: t.registration.manual, tone: 'success' }
    case WebhookRegistrationState.Failed:
      return { label: t.registration.failed, tone: 'danger' }
    default:
      return { label: t.registration.pending, tone: 'warning' }
  }
}

function resultView(result: WebhookProcessResult, t: WebhooksDictionary): { label: string; tone: BadgeTone } {
  switch (result) {
    case WebhookProcessResult.Processed:
      return { label: t.result.processed, tone: 'success' }
    case WebhookProcessResult.Ignored:
      return { label: t.result.ignored, tone: 'neutral' }
    case WebhookProcessResult.Failed:
      return { label: t.result.failed, tone: 'danger' }
    case WebhookProcessResult.InvalidSignature:
      return { label: t.result.invalidSignature, tone: 'danger' }
    case WebhookProcessResult.Stale:
      return { label: t.result.stale, tone: 'neutral' }
    default:
      return { label: t.result.pending, tone: 'info' }
  }
}

function deliveryView(status: WebhookDeliveryStatus, t: WebhooksDictionary): { label: string; tone: BadgeTone } {
  switch (status) {
    case WebhookDeliveryStatus.Succeeded:
      return { label: t.delivery.succeeded, tone: 'success' }
    case WebhookDeliveryStatus.Failed:
      return { label: t.delivery.failed, tone: 'danger' }
    case WebhookDeliveryStatus.Retrying:
      return { label: t.delivery.retrying, tone: 'warning' }
    default:
      return { label: t.delivery.pending, tone: 'info' }
  }
}

function pageRequest(params: URLSearchParams, prefix: string, sortBy: string): ListRequest {
  const page = Number(params.get(`${prefix}page`))
  const size = Number(params.get(`${prefix}size`))
  return {
    PageNumber: Number.isInteger(page) && page > 0 ? page : 1,
    RowCount: Number.isInteger(size) && size > 0 ? size : DEFAULT_PAGE_SIZE,
    Sorting: { SortBy: sortBy, Type: SortType.Desc },
  }
}

async function copyText(value: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(value)
    return true
  } catch {
    return false
  }
}

function CopyButton({ value, label }: { value: string; label: string }) {
  const base = useT()
  return (
    <IconButton
      label={label}
      size="sm"
      ghost
      onClick={async () => {
        if (await copyText(value)) {
          showToast(base.common.copied, 'success')
        } else {
          showToast(base.common.copyFailed, 'error')
        }
      }}
    >
      <Copy aria-hidden />
    </IconButton>
  )
}

/** The secret of a new or rotated endpoint or subscription: shown in this dialog only. */
function SecretDialog({
  secret,
  title,
  manual,
  google,
  onClose,
}: {
  secret: WebhookSecretDto
  title: string
  manual?: boolean
  google?: boolean
  onClose: () => void
}) {
  const t = useWebhooksText()
  const base = useT()
  const format = useFormat()
  return (
    <Modal
      open
      onClose={onClose}
      title={title}
      description={t.secret.once}
      footer={
        <Button variant="primary" onClick={onClose}>
          {base.common.close}
        </Button>
      }
    >
      <div className="grid gap-3">
        {secret.Url ? (
          <div className="grid gap-1">
            <b className="text-[13px]">{t.secret.url}</b>
            <SecretBox value={secret.Url} />
          </div>
        ) : null}
        {secret.Secret ? (
          <div className="grid gap-1">
            <b className="text-[13px]">{t.secret.value}</b>
            <SecretBox value={secret.Secret} />
          </div>
        ) : null}
        {secret.PreviousSecretValidUntil ? (
          <p className="text-[13px] text-fg2">{interpolate(t.secret.grace, { date: format.dateTime(secret.PreviousSecretValidUntil) })}</p>
        ) : null}
        {manual ? <Callout tone="warning">{t.secret.manualSteps}</Callout> : null}
        {google ? <Callout tone="info">{t.secret.googleSteps}</Callout> : null}
      </div>
    </Modal>
  )
}

// ---- Inbound ----------------------------------------------------------------------------------------------------------

function EndpointDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (secret: WebhookSecretDto) => void }) {
  const t = useWebhooksText()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
  })
  const apple = (connections.data ?? []).filter((connection) => connection.Store === StoreKind.Apple)
  const [source, setSource] = useState<WebhookSource>(WebhookSource.AppStoreConnect)
  const [connectionId, setConnectionId] = useState<number | null>(null)
  const [name, setName] = useState('')
  const [register, setRegister] = useState(true)
  const [serviceAccount, setServiceAccount] = useState('')
  const create = useMutation({
    mutationFn: () =>
      webhooksApi.createEndpoint(organization.Id, {
        Source: source,
        StoreConnectionId: source === WebhookSource.AppStoreConnect ? connectionId : null,
        Name: source === WebhookSource.AppStoreConnect ? null : name.trim() || null,
        RegisterAtStore: source === WebhookSource.AppStoreConnect && register,
        PushServiceAccount: source === WebhookSource.GooglePlay ? serviceAccount.trim() : null,
      }),
    onSuccess: (secret) => {
      void queryClient.invalidateQueries({ queryKey: webhookKeys.all })
      onCreated(secret)
    },
  })
  const ready =
    source === WebhookSource.AppStoreConnect
      ? connectionId !== null
      : source === WebhookSource.GooglePlay
        ? serviceAccount.trim().includes('@')
        : name.trim().length > 0
  return (
    <Modal
      open
      onClose={onClose}
      title={t.endpoints.addTitle}
      description={t.endpoints.addBody}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={create.isPending} disabled={!ready} onClick={() => create.mutate()}>
            {t.endpoints.create}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <RadioGroup
          name="endpoint-source"
          label={t.endpoints.source}
          value={source}
          options={[
            { value: WebhookSource.AppStoreConnect, label: t.source.apple, description: t.source.appleHint },
            { value: WebhookSource.GooglePlay, label: t.source.google, description: t.source.googleHint },
            { value: WebhookSource.Signed, label: t.source.signed, description: t.source.signedHint },
          ]}
          onChange={setSource}
        />
        {source === WebhookSource.AppStoreConnect ? (
          <>
            <Field label={t.endpoints.connection} hint={apple.length === 0 ? t.endpoints.noConnection : undefined}>
              <Select<number>
                value={connectionId}
                placeholder={t.endpoints.pickConnection}
                options={apple.map((connection) => ({ value: connection.Id, label: connection.Name }))}
                onChange={setConnectionId}
              />
            </Field>
            <Checkbox checked={register} onChange={setRegister}>
              {t.endpoints.registerAtStore}
            </Checkbox>
          </>
        ) : source === WebhookSource.GooglePlay ? (
          <Field label={t.endpoints.serviceAccount} hint={t.endpoints.serviceAccountHint}>
            <Input
              type="email"
              value={serviceAccount}
              maxLength={320}
              placeholder="play-rtdn@project.iam.gserviceaccount.com"
              onChange={(event) => setServiceAccount(event.target.value)}
            />
          </Field>
        ) : (
          <Field label={t.endpoints.name}>
            <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
          </Field>
        )}
      </div>
    </Modal>
  )
}

function EndpointCard({ endpoint, onSecret }: { endpoint: WebhookEndpointDto; onSecret: (secret: WebhookSecretDto) => void }) {
  const t = useWebhooksText()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const confirm = useConfirm()
  const view = registrationView(endpoint.Registration, t)
  const title = endpoint.ConnectionName ?? endpoint.Name ?? `#${endpoint.Id}`
  const rotate = useMutation({
    mutationFn: () => webhooksApi.rotateEndpointSecret(organization.Id, endpoint.Id),
    onSuccess: (secret) => {
      void queryClient.invalidateQueries({ queryKey: webhookKeys.all })
      onSecret(secret)
    },
  })
  const ping = useMutation({
    mutationFn: () => webhooksApi.pingEndpoint(organization.Id, endpoint.Id),
    onSuccess: () => showToast(t.endpoints.pinged, 'success'),
  })
  const remove = useMutation({
    mutationFn: () => webhooksApi.deleteEndpoint(organization.Id, endpoint.Id),
    onSuccess: () => {
      showToast(t.endpoints.deleted, 'success')
      void queryClient.invalidateQueries({ queryKey: webhookKeys.all })
    },
  })
  return (
    <li className="card grid gap-2 p-4">
      <span className="flex flex-wrap items-center gap-2">
        {endpoint.Source === WebhookSource.AppStoreConnect ? (
          <StoreTag store={StoreKind.Apple} small codeOnly />
        ) : endpoint.Source === WebhookSource.GooglePlay ? (
          <StoreTag store={StoreKind.Google} small codeOnly />
        ) : (
          <Badge small>{t.source.signed}</Badge>
        )}
        <b className="text-[14px]">{title}</b>
        <Badge tone={view.tone} dot small>
          {view.label}
        </Badge>
      </span>
      {endpoint.Url ? (
        <span className="flex items-center gap-1">
          <code className="mono min-w-0 flex-1 break-all text-[13px]">{endpoint.Url}</code>
          <CopyButton value={endpoint.Url} label={interpolate(t.endpoints.copyUrl, { name: title })} />
        </span>
      ) : null}
      {endpoint.RegistrationError ? <Callout tone="danger">{endpoint.RegistrationError}</Callout> : null}
      <dl className="grid gap-1 text-[13px] sm:grid-cols-2">
        <div>
          <dt className="text-xs text-fg3">{t.endpoints.events}</dt>
          <dd className="mono break-all">{(endpoint.Events ?? []).join(', ') || t.endpoints.allEvents}</dd>
        </div>
        {endpoint.Source === WebhookSource.GooglePlay ? (
          <div>
            <dt className="text-xs text-fg3">{t.endpoints.serviceAccount}</dt>
            <dd className="mono break-all">{endpoint.PushServiceAccount ?? '-'}</dd>
          </div>
        ) : (
          <div>
            <dt className="text-xs text-fg3">{t.endpoints.secret}</dt>
            <dd>
              {interpolate(t.endpoints.secretSet, { date: format.date(endpoint.SecretSetDate) })}
              {endpoint.PreviousSecretValidUntil
                ? ` · ${interpolate(t.endpoints.previousValid, { date: format.dateTime(endpoint.PreviousSecretValidUntil) })}`
                : ''}
            </dd>
          </div>
        )}
        <div>
          <dt className="text-xs text-fg3">{t.endpoints.lastEvent}</dt>
          <dd>{endpoint.LastEventDate ? format.dateTime(endpoint.LastEventDate) : '-'}</dd>
        </div>
        {endpoint.Source === WebhookSource.AppStoreConnect ? (
          <div>
            <dt className="text-xs text-fg3">{t.endpoints.appleWebhooks}</dt>
            <dd className="tabular-nums">{endpoint.ExternalWebhookCount}</dd>
          </div>
        ) : null}
      </dl>
      <span className="flex flex-wrap justify-end gap-2">
        {endpoint.Source === WebhookSource.AppStoreConnect && endpoint.ExternalWebhookCount > 0 ? (
          <Button size="sm" loading={ping.isPending} onClick={() => ping.mutate()}>
            <Send aria-hidden />
            {t.endpoints.ping}
          </Button>
        ) : null}
        {endpoint.Source === WebhookSource.GooglePlay ? null : (
          <Button
            size="sm"
            loading={rotate.isPending}
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.endpoints.rotateTitle, { name: title }),
                  message: t.endpoints.rotateBody,
                  confirmLabel: t.endpoints.rotate,
                })
              ) {
                rotate.mutate()
              }
            }}
          >
            <RefreshCw aria-hidden />
            {t.endpoints.rotate}
          </Button>
        )}
        <Button
          size="sm"
          variant="danger-outline"
          loading={remove.isPending}
          onClick={async () => {
            if (
              await confirm({
                title: interpolate(t.endpoints.deleteTitle, { name: title }),
                message: t.endpoints.deleteBody,
                danger: true,
                confirmLabel: t.endpoints.delete,
              })
            ) {
              remove.mutate()
            }
          }}
        >
          <Trash2 aria-hidden />
          {t.endpoints.delete}
        </Button>
      </span>
    </li>
  )
}

function relatedLink(event: { RelatedEntityType: NotificationEntity; RelatedEntityId?: string; AppId?: number }): string | null {
  if (event.RelatedEntityType === NotificationEntity.RejectionCase && event.RelatedEntityId && event.AppId) {
    return `/apps/${event.AppId}/rejections/${event.RelatedEntityId}`
  }
  if (event.AppId) {
    return `/apps/${event.AppId}`
  }
  return null
}

function EventDrawer({ eventId, onClose }: { eventId: number; onClose: () => void }) {
  const t = useWebhooksText()
  const base = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const detail = useQuery({
    queryKey: webhookKeys.event(organization.Id, eventId),
    queryFn: () => webhooksApi.event(organization.Id, eventId),
  })
  const reprocess = useMutation({
    mutationFn: () => webhooksApi.reprocessEvent(organization.Id, eventId),
    onSuccess: () => {
      showToast(t.events.reprocessed, 'success')
      void queryClient.invalidateQueries({ queryKey: webhookKeys.all })
    },
  })
  const event = detail.data
  const link = event ? relatedLink(event) : null
  return (
    <Modal
      open
      onClose={onClose}
      variant="drawer"
      size="lg"
      title={event ? `${event.OldValue ?? '-'} → ${event.NewValue ?? '-'}` : t.events.detail}
      description={event ? format.dateTime(event.ReceivedDate) : undefined}
      footer={
        event ? (
          <>
            <Button variant="ghost" onClick={onClose}>
              {base.common.close}
            </Button>
            {event.Payload ? (
              <Button
                onClick={async () => {
                  if (await copyText(event.Payload ?? '')) {
                    showToast(base.common.copied, 'success')
                  } else {
                    showToast(base.common.copyFailed, 'error')
                  }
                }}
              >
                {t.events.copyBody}
              </Button>
            ) : null}
            {event.SignatureValid ? (
              <Button variant="primary" loading={reprocess.isPending} onClick={() => reprocess.mutate()}>
                {t.events.reprocess}
              </Button>
            ) : null}
          </>
        ) : undefined
      }
    >
      {detail.isLoading ? (
        <CardSkeleton count={1} />
      ) : detail.isError || !event ? (
        <ScreenState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : (
        <div className="grid gap-3 text-[13px]">
          <span className="flex flex-wrap gap-2">
            <Badge tone={event.SignatureValid ? 'success' : 'danger'} small>
              {event.SignatureValid ? t.events.signatureValid : t.events.signatureInvalid}
            </Badge>
            <Badge tone={resultView(event.ProcessResult, t).tone} small>
              {resultView(event.ProcessResult, t).label}
            </Badge>
          </span>
          <dl className="grid gap-1.5">
            <div>
              <dt className="text-xs text-fg3">{t.events.type}</dt>
              <dd className="mono break-all">{event.EventType ?? '-'}</dd>
            </div>
            <div>
              <dt className="text-xs text-fg3">{t.events.signatureHeader}</dt>
              <dd className="mono break-all">{event.SignatureHeaderMasked ?? '-'}</dd>
            </div>
            <div>
              <dt className="text-xs text-fg3">{t.events.processing}</dt>
              <dd>
                {[event.ProcessedDate ? format.dateTime(event.ProcessedDate) : null, event.ProcessMessage].filter(Boolean).join(' · ') ||
                  '-'}
                {link ? (
                  <>
                    {' · '}
                    <Link className="lnk" to={link}>
                      {t.events.openRelated}
                    </Link>
                  </>
                ) : null}
              </dd>
            </div>
          </dl>
          {!event.SignatureValid ? (
            <Callout tone="danger" title={t.events.invalidTitle}>
              {t.events.invalidBody}
            </Callout>
          ) : null}
          {event.Payload ? (
            <div className="grid gap-1">
              <b>{t.events.payload}</b>
              <pre className="code whitespace-pre-wrap break-all">{event.Payload}</pre>
            </div>
          ) : null}
        </div>
      )}
    </Modal>
  )
}

function Inbound({ params, update }: { params: URLSearchParams; update: (changes: Record<string, string | null>) => void }) {
  const t = useWebhooksText()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const [adding, setAdding] = useState(false)
  const [secret, setSecret] = useState<{ value: WebhookSecretDto; title: string } | null>(null)
  const request = useMemo(() => pageRequest(params, 'e', 'ReceivedDate'), [params])
  const endpoints = useQuery({
    queryKey: webhookKeys.endpoints(organization.Id),
    queryFn: () => webhooksApi.endpoints(organization.Id),
  })
  const events = useQuery({
    queryKey: webhookKeys.events(organization.Id, request),
    queryFn: () => webhooksApi.events(organization.Id, request),
    placeholderData: keepPreviousData,
  })
  const eventId = Number(params.get('event'))
  const columns: Column<WebhookEventDto>[] = [
    {
      key: 'received',
      header: t.events.received,
      cell: (event) => <span className="whitespace-nowrap tabular-nums">{format.dateTime(event.ReceivedDate)}</span>,
    },
    {
      key: 'change',
      header: t.events.change,
      role: 'main',
      cell: (event) =>
        event.OldValue || event.NewValue ? `${event.OldValue ?? '-'} → ${event.NewValue ?? '-'}` : (event.EventType ?? '-'),
    },
    {
      key: 'signature',
      header: t.events.signature,
      cell: (event) => (
        <Badge tone={event.SignatureValid ? 'success' : 'danger'} small>
          {event.SignatureValid ? t.events.signatureValid : t.events.signatureInvalid}
        </Badge>
      ),
    },
    {
      key: 'result',
      header: t.events.result,
      cell: (event) => (
        <Badge tone={resultView(event.ProcessResult, t).tone} small>
          {resultView(event.ProcessResult, t).label}
        </Badge>
      ),
    },
    {
      key: 'message',
      header: t.events.message,
      role: 'hidePhone',
      cell: (event) => <span className="text-fg2">{event.ProcessMessage ?? '-'}</span>,
    },
  ]
  const rows = events.data?.Data ?? []
  const endpointList = endpoints.data ?? []
  const manual = (value: WebhookSecretDto) =>
    value.Registration === WebhookRegistrationState.Failed || value.Registration === WebhookRegistrationState.Manual

  return (
    <>
      <div className="mb-3 flex items-center gap-2">
        <p className="mr-auto text-[13px] text-fg2">{t.endpoints.intro}</p>
        <Button variant="primary" onClick={() => setAdding(true)}>
          <Plus aria-hidden />
          {t.endpoints.add}
        </Button>
      </div>
      {endpoints.isLoading ? (
        <CardSkeleton />
      ) : endpoints.isError ? (
        <ScreenState error={endpoints.error} onRetry={() => void endpoints.refetch()} />
      ) : endpointList.length === 0 ? (
        <EmptyState icon={<Webhook aria-hidden />} title={t.endpoints.emptyTitle} description={t.endpoints.emptyBody} />
      ) : (
        <ul className="grid gap-3 lg:grid-cols-2">
          {endpointList.map((endpoint) => (
            <EndpointCard key={endpoint.Id} endpoint={endpoint} onSecret={(value) => setSecret({ value, title: t.secret.rotatedTitle })} />
          ))}
        </ul>
      )}
      <h2 className="mb-2 mt-5 text-[15px] font-semibold">{t.events.title}</h2>
      {events.isLoading ? (
        <CardSkeleton />
      ) : events.isError ? (
        <ScreenState error={events.error} onRetry={() => void events.refetch()} />
      ) : rows.length === 0 ? (
        <p className="text-[13px] text-fg3">{t.events.empty}</p>
      ) : (
        <>
          <DataTable
            caption={t.events.title}
            columns={columns}
            rows={rows}
            rowKey={(event) => event.Id}
            busy={events.isFetching}
            onRowClick={(event) => update({ event: String(event.Id) })}
          />
          <Pagination
            page={request.PageNumber}
            pageCount={events.data?.TotalPageCount ?? 1}
            total={events.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => update({ epage: String(page) })}
            onPageSize={(size) => update({ esize: String(size), epage: null })}
          />
        </>
      )}
      {adding ? (
        <EndpointDialog
          onClose={() => setAdding(false)}
          onCreated={(value) => {
            setAdding(false)
            setSecret({ value, title: t.secret.createdTitle })
          }}
        />
      ) : null}
      {secret ? (
        <SecretDialog
          secret={secret.value}
          title={secret.title}
          manual={manual(secret.value)}
          google={secret.value.Url?.includes('/api/webhooks/google/') ?? false}
          onClose={() => setSecret(null)}
        />
      ) : null}
      {Number.isInteger(eventId) && eventId > 0 ? <EventDrawer eventId={eventId} onClose={() => update({ event: null })} /> : null}
    </>
  )
}

// ---- Outgoing ---------------------------------------------------------------------------------------------------------

function SubscriptionDialog({
  subscription,
  onClose,
  onCreated,
}: {
  subscription: OrgWebhookDto | null
  onClose: () => void
  onCreated: (secret: WebhookSecretDto) => void
}) {
  const t = useWebhooksText()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [name, setName] = useState(subscription?.Name ?? '')
  const [url, setUrl] = useState(subscription?.Url ?? '')
  const [events, setEvents] = useState<string[]>(subscription?.Events ?? [])
  const urlError = url && !/^https:\/\/[^\s]+$/i.test(url.trim()) ? t.subscriptions.httpsOnly : null
  const save = useMutation({
    mutationFn: async () => {
      const input = { Name: name.trim(), Url: url.trim(), Events: events }
      if (subscription) {
        await webhooksApi.updateSubscription(organization.Id, subscription.Id, input)
        return null
      }
      return webhooksApi.createSubscription(organization.Id, input)
    },
    onSuccess: (secret) => {
      void queryClient.invalidateQueries({ queryKey: webhookKeys.all })
      if (secret) {
        onCreated(secret)
      } else {
        showToast(t.subscriptions.saved, 'success')
        onClose()
      }
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={subscription ? t.subscriptions.editTitle : t.subscriptions.addTitle}
      description={t.subscriptions.addBody}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button
            variant="primary"
            loading={save.isPending}
            disabled={!name.trim() || !url.trim() || !!urlError}
            onClick={() => save.mutate()}
          >
            {base.common.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.subscriptions.name}>
          <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
        </Field>
        <Field label={t.subscriptions.url} error={urlError} hint={t.subscriptions.urlHint}>
          <Input type="url" value={url} placeholder="https://" onChange={(event) => setUrl(event.target.value)} />
        </Field>
        <div className="grid gap-1.5" role="group" aria-label={t.subscriptions.events}>
          <b className="text-[13px]">{t.subscriptions.events}</b>
          <span className="text-xs text-fg3">{t.subscriptions.eventsHint}</span>
          {ORG_WEBHOOK_EVENTS.map((event) => (
            <Checkbox
              key={event}
              checked={events.includes(event)}
              onChange={(checked) => setEvents((list) => (checked ? [...list, event] : list.filter((item) => item !== event)))}
            >
              <span className="mono">{event}</span>
            </Checkbox>
          ))}
        </div>
      </div>
    </Modal>
  )
}

function Outgoing({ params, update }: { params: URLSearchParams; update: (changes: Record<string, string | null>) => void }) {
  const t = useWebhooksText()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const confirm = useConfirm()
  const [dialog, setDialog] = useState<OrgWebhookDto | 'new' | null>(null)
  const [secret, setSecret] = useState<{ value: WebhookSecretDto; title: string } | null>(null)
  const request = useMemo(() => pageRequest(params, 'd', 'CreationDate'), [params])
  const filterId = Number(params.get('sub'))
  const webhookId = Number.isInteger(filterId) && filterId > 0 ? filterId : null
  const subscriptions = useQuery({
    queryKey: webhookKeys.subscriptions(organization.Id),
    queryFn: () => webhooksApi.subscriptions(organization.Id),
  })
  const deliveries = useQuery({
    queryKey: webhookKeys.deliveries(organization.Id, webhookId, request),
    queryFn: () => webhooksApi.deliveries(organization.Id, webhookId, request),
    placeholderData: keepPreviousData,
  })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: webhookKeys.all })
  const toggle = useMutation({
    mutationFn: (subscription: OrgWebhookDto) =>
      webhooksApi.updateSubscription(organization.Id, subscription.Id, { IsActive: !subscription.IsActive }),
    onSuccess: refresh,
  })
  const rotate = useMutation({
    mutationFn: (subscription: OrgWebhookDto) => webhooksApi.rotateSubscriptionSecret(organization.Id, subscription.Id),
    onSuccess: (value) => {
      refresh()
      setSecret({ value, title: t.secret.rotatedTitle })
    },
  })
  const test = useMutation({
    mutationFn: (subscription: OrgWebhookDto) => webhooksApi.testSubscription(organization.Id, subscription.Id),
    onSuccess: (delivery) => {
      refresh()
      const view = deliveryView(delivery.Status, t)
      showToast(
        interpolate(t.subscriptions.tested, { status: view.label }),
        delivery.Status === WebhookDeliveryStatus.Failed ? 'error' : 'success',
        delivery.ResponseStatusCode ? `HTTP ${delivery.ResponseStatusCode}` : (delivery.Error ?? undefined),
      )
    },
  })
  const remove = useMutation({
    mutationFn: (subscription: OrgWebhookDto) => webhooksApi.deleteSubscription(organization.Id, subscription.Id),
    onSuccess: () => {
      showToast(t.subscriptions.deleted, 'success')
      refresh()
    },
  })
  const list = subscriptions.data ?? []
  const nameOf = (id: number) => list.find((item) => item.Id === id)?.Name ?? `#${id}`

  const subscriptionColumns: Column<OrgWebhookDto>[] = [
    {
      key: 'name',
      header: t.subscriptions.name,
      role: 'main',
      cell: (item) => (
        <span className="grid">
          <b>{item.Name}</b>
          <span className="mono break-all text-xs text-fg3">{item.Url}</span>
        </span>
      ),
    },
    {
      key: 'events',
      header: t.subscriptions.events,
      cell: (item) => <span className="mono text-xs">{(item.Events ?? []).join(', ') || t.endpoints.allEvents}</span>,
    },
    {
      key: 'last',
      header: t.subscriptions.last,
      cell: (item) =>
        item.LastDeliveryStatus ? (
          <span className="grid gap-0.5">
            <Badge tone={deliveryView(item.LastDeliveryStatus, t).tone} small>
              {deliveryView(item.LastDeliveryStatus, t).label}
            </Badge>
            <span className="text-xs text-fg3">{format.dateTime(item.LastDeliveryDate)}</span>
          </span>
        ) : (
          '-'
        ),
    },
    {
      key: 'active',
      header: t.subscriptions.active,
      cell: (item) => (
        <Switch
          checked={item.IsActive}
          label={interpolate(t.subscriptions.activeOf, { name: item.Name ?? '' })}
          onChange={() => toggle.mutate(item)}
        />
      ),
    },
    {
      key: 'actions',
      header: t.subscriptions.actions,
      role: 'action',
      cell: (item) => (
        <span className="flex gap-1">
          <IconButton
            label={interpolate(t.subscriptions.testOf, { name: item.Name ?? '' })}
            size="sm"
            ghost
            onClick={() => test.mutate(item)}
          >
            <Send aria-hidden />
          </IconButton>
          <IconButton
            label={interpolate(t.subscriptions.editOf, { name: item.Name ?? '' })}
            size="sm"
            ghost
            onClick={() => setDialog(item)}
          >
            <Pencil aria-hidden />
          </IconButton>
          <IconButton
            label={interpolate(t.subscriptions.rotateOf, { name: item.Name ?? '' })}
            size="sm"
            ghost
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.subscriptions.rotateTitle, { name: item.Name ?? '' }),
                  message: t.subscriptions.rotateBody,
                  confirmLabel: t.endpoints.rotate,
                })
              ) {
                rotate.mutate(item)
              }
            }}
          >
            <RefreshCw aria-hidden />
          </IconButton>
          <IconButton
            label={interpolate(t.subscriptions.deleteOf, { name: item.Name ?? '' })}
            size="sm"
            ghost
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.subscriptions.deleteTitle, { name: item.Name ?? '' }),
                  message: t.subscriptions.deleteBody,
                  danger: true,
                  confirmLabel: t.endpoints.delete,
                })
              ) {
                remove.mutate(item)
              }
            }}
          >
            <Trash2 aria-hidden />
          </IconButton>
        </span>
      ),
    },
  ]

  const deliveryColumns: Column<WebhookDeliveryDto>[] = [
    {
      key: 'date',
      header: t.delivery.date,
      cell: (item) => <span className="whitespace-nowrap tabular-nums">{format.dateTime(item.CreationDate)}</span>,
    },
    { key: 'subscription', header: t.delivery.subscription, role: 'main', cell: (item) => nameOf(item.OrganizationWebhookId) },
    {
      key: 'event',
      header: t.delivery.event,
      cell: (item) => (
        <span className="mono text-xs">
          {item.EventType}
          {item.IsTest ? ` · ${t.delivery.test}` : ''}
        </span>
      ),
    },
    {
      key: 'status',
      header: t.delivery.status,
      cell: (item) => (
        <Badge tone={deliveryView(item.Status, t).tone} small>
          {deliveryView(item.Status, t).label}
        </Badge>
      ),
    },
    {
      key: 'attempt',
      header: t.delivery.attempt,
      cell: (item) => <span className="tabular-nums">{`${item.Attempt} / ${item.MaxAttempts}`}</span>,
    },
    {
      key: 'response',
      header: t.delivery.response,
      role: 'hidePhone',
      cell: (item) =>
        [
          item.ResponseStatusCode ? `HTTP ${item.ResponseStatusCode}` : null,
          item.DurationMilliseconds !== undefined ? `${item.DurationMilliseconds} ms` : null,
          item.Error,
        ]
          .filter(Boolean)
          .join(' · ') || '-',
    },
  ]
  const rows = deliveries.data?.Data ?? []

  return (
    <>
      <div className="mb-3 flex items-center gap-2">
        <p className="mr-auto text-[13px] text-fg2">{t.subscriptions.intro}</p>
        <Button variant="primary" onClick={() => setDialog('new')}>
          <Plus aria-hidden />
          {t.subscriptions.add}
        </Button>
      </div>
      {subscriptions.isLoading ? (
        <CardSkeleton />
      ) : subscriptions.isError ? (
        <ScreenState error={subscriptions.error} onRetry={() => void subscriptions.refetch()} />
      ) : list.length === 0 ? (
        <EmptyState icon={<Send aria-hidden />} title={t.subscriptions.emptyTitle} description={t.subscriptions.emptyBody} />
      ) : (
        <DataTable caption={t.subscriptions.title} columns={subscriptionColumns} rows={list} rowKey={(item) => item.Id} />
      )}
      <div className="mb-2 mt-5 flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-[15px] font-semibold">{t.delivery.title}</h2>
        <span className="w-full sm:w-56">
          <Select
            aria-label={t.delivery.subscription}
            value={webhookId === null ? 'all' : String(webhookId)}
            options={[
              { value: 'all', label: t.delivery.allSubscriptions },
              ...list.map((item) => ({ value: String(item.Id), label: item.Name ?? `#${item.Id}` })),
            ]}
            onChange={(value) => update({ sub: value === 'all' ? null : value, dpage: null })}
          />
        </span>
      </div>
      {deliveries.isLoading ? (
        <CardSkeleton />
      ) : deliveries.isError ? (
        <ScreenState error={deliveries.error} onRetry={() => void deliveries.refetch()} />
      ) : rows.length === 0 ? (
        <p className="text-[13px] text-fg3">{t.delivery.empty}</p>
      ) : (
        <>
          <DataTable
            caption={t.delivery.title}
            columns={deliveryColumns}
            rows={rows}
            rowKey={(item) => item.Id}
            busy={deliveries.isFetching}
          />
          <Pagination
            page={request.PageNumber}
            pageCount={deliveries.data?.TotalPageCount ?? 1}
            total={deliveries.data?.TotalDataCount ?? rows.length}
            pageSize={request.RowCount}
            onPage={(page) => update({ dpage: String(page) })}
            onPageSize={(size) => update({ dsize: String(size), dpage: null })}
          />
        </>
      )}
      {dialog ? (
        <SubscriptionDialog
          subscription={dialog === 'new' ? null : dialog}
          onClose={() => setDialog(null)}
          onCreated={(value) => {
            setDialog(null)
            setSecret({ value, title: t.secret.createdTitle })
          }}
        />
      ) : null}
      {secret ? <SecretDialog secret={secret.value} title={secret.title} onClose={() => setSecret(null)} /> : null}
    </>
  )
}

/** /webhooks: owners and admins (ManageWebhooks). */
export function WebhooksPage() {
  const t = useWebhooksText()
  const base = useT()
  const { can } = useOrganization()
  const [params, setParams] = useSearchParams()
  const tab: Tab = params.get('tab') === 'outgoing' ? 'outgoing' : 'inbound'

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') {
        next.delete(key)
      } else {
        next.set(key, value)
      }
    }
    setParams(next, { replace: true })
  }

  if (!can(OrgPermission.ManageWebhooks)) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={base.states.forbiddenTitle}
        description={t.forbidden}
        action={
          <Link className="btn inv" to={HOME_PATH}>
            {base.states.backToApps}
          </Link>
        }
      />
    )
  }

  return (
    <>
      <PageHeader title={t.title} subtitle={t.subtitle} />
      <Tabs<Tab>
        label={t.title}
        value={tab}
        items={[
          { value: 'inbound', label: t.tabs.inbound },
          { value: 'outgoing', label: t.tabs.outgoing },
        ]}
        onChange={(value) => setParams(value === 'outgoing' ? { tab: 'outgoing' } : {}, { replace: true })}
      />
      <div className="mt-3">
        {tab === 'inbound' ? <Inbound params={params} update={update} /> : <Outgoing params={params} update={update} />}
      </div>
    </>
  )
}
