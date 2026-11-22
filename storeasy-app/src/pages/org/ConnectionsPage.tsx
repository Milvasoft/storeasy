import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plug, Plus, ShieldAlert, Trash2, RefreshCw } from 'lucide-react'
import { AddConnectionDialog } from '@/components/connections/AddConnectionDialog'
import { ConnectionStatusBadge } from '@/components/connections/ConnectionStatusBadge'
import { EditConnectionDialog } from '@/components/connections/EditConnectionDialog'
import { canReplaceKey, ReplaceKeyButton, ReplaceKeyDialog } from '@/components/connections/ReplaceKeyDialog'
import { StoreTag } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { ScreenState, StateView } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, EmptyState, PageHeader } from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { ConnectionStatus, OrgPermission, StoreKind, type StoreConnectionDto } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { cn } from '@/lib/utils/cn'
import {
  MsExpiringBanner,
  MsFingerprint,
  RenewSecretButton,
  RenewSecretDialog,
  SecretExpiryBadge,
  secretExpiring,
} from '@/features/microsoft/connections'
import { ALL_STORES, channelOf } from '@/lib/stores/channels'
import { useWinget } from '@/features/winget/i18n/useWinget'
import { useChrome } from '@/features/chrome/i18n/useChrome'

function Fingerprint({ connection }: { connection: StoreConnectionDto }) {
  const t = useT()
  const tw = useWinget()
  const tc = useChrome()
  if (connection.Store === StoreKind.Microsoft) {
    return <MsFingerprint connection={connection} />
  }
  const fingerprint = connection.Fingerprint ?? {}
  const rows: Array<[string, string | null | undefined]> =
    connection.Store === StoreKind.Winget
      ? [
          [tw.connection.fork, fingerprint.Repository],
          [tw.connection.tokenKind, fingerprint.TokenKind],
          [tw.connection.expiry, connection.CredentialExpiryDate ? connection.CredentialExpiryDate.slice(0, 10) : null],
        ]
      : connection.Store === StoreKind.Chrome
        ? [
            [tc.connection.publisherId, fingerprint.PublisherId],
            [tc.connection.auth, fingerprint.TokenKind],
            [
              fingerprint.ServiceAccountEmail ? t.connections.serviceAccount : tc.connection.clientId,
              fingerprint.ServiceAccountEmail ?? (fingerprint.ClientIdSuffix ? `…${fingerprint.ClientIdSuffix}` : null),
            ],
          ]
        : connection.Store === StoreKind.Apple
          ? [
              [t.connections.keyId, fingerprint.KeyId],
              [t.connections.issuerId, fingerprint.IssuerIdSuffix ? `…${fingerprint.IssuerIdSuffix}` : null],
            ]
          : [
              [t.connections.serviceAccount, fingerprint.ServiceAccountEmail],
              [t.connections.project, fingerprint.ProjectId],
            ]
  return (
    <dl className="kv">
      {rows.map(([label, value]) => (
        <div key={label} className="contents">
          <dt>{label}</dt>
          <dd className="mono text-[13px]">{value || '-'}</dd>
        </div>
      ))}
    </dl>
  )
}

function ConnectionCard({
  connection,
  organizationId,
  onRenew,
  onEdit,
  onReplaceKey,
}: {
  connection: StoreConnectionDto
  organizationId: number
  onRenew: (connection: StoreConnectionDto) => void
  onEdit: (connection: StoreConnectionDto) => void
  onReplaceKey: (connection: StoreConnectionDto) => void
}) {
  const t = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const queryClient = useQueryClient()

  const verify = useMutation({
    mutationFn: () => connectionsApi.verify(organizationId, connection.Id),
    onSuccess: (updated) => {
      queryClient.setQueryData<StoreConnectionDto[]>(queryKeys.connections.list(organizationId), (list) =>
        list?.map((item) => (item.Id === updated.Id ? updated : item)),
      )
      if (updated.Status === ConnectionStatus.Verified) {
        showToast(t.connections.verifiedToast, 'success', updated.Name)
      } else {
        showToast(t.connections.verifyFailed, 'error', updated.LastVerifyError ?? undefined)
      }
    },
  })

  const remove = useMutation({
    mutationFn: () => connectionsApi.remove(organizationId, connection.Id),
    onSuccess: () => {
      showToast(t.connections.removed, 'success', connection.Name)
      void queryClient.invalidateQueries({ queryKey: queryKeys.connections.all })
    },
  })

  const askRemove = async () => {
    const ok = await confirm({
      title: interpolate(t.connections.removeTitle, { name: connection.Name }),
      message: t.connections.removeBody,
      confirmLabel: t.connections.remove,
      danger: true,
      typeToConfirm: connection.Name,
    })
    if (ok) {
      remove.mutate()
    }
  }

  const failed = connection.Status === ConnectionStatus.Rejected || connection.Status === ConnectionStatus.Unreachable
  return (
    <article
      className={cn(
        'card flex flex-col',
        connection.Status === ConnectionStatus.Rejected && 'dng',
        (connection.Status === ConnectionStatus.Unreachable || secretExpiring(connection)) && 'warn',
      )}
      aria-label={connection.Name}
    >
      <div className="card-h">
        <StoreTag store={connection.Store} connectionId={connection.Id} />
        <h2 className="h3 min-w-0 flex-1 truncate">{connection.Name}</h2>
        <SecretExpiryBadge connection={connection} />
        <ConnectionStatusBadge status={connection.Status} verifying={verify.isPending} />
      </div>
      <div className="card-b grid flex-1 gap-3">
        <Fingerprint connection={connection} />
        {failed && connection.LastVerifyError ? (
          <p className="m-0 rounded-md bg-bg2 p-2.5 text-[12.5px] text-fg2">{connection.LastVerifyError}</p>
        ) : null}
        <p className="m-0 text-xs text-fg3">
          {connection.LastVerifiedDate
            ? interpolate(t.connections.lastVerified, { date: format.dateTime(connection.LastVerifiedDate) })
            : t.connections.neverVerified}
        </p>
      </div>
      <div className="card-f">
        <Button size="sm" loading={verify.isPending} onClick={() => verify.mutate()}>
          <RefreshCw aria-hidden />
          {t.connections.verify}
        </Button>
        <Button size="sm" onClick={() => onEdit(connection)}>
          <Pencil aria-hidden />
          {t.connections.edit}
        </Button>
        {connection.Store === StoreKind.Microsoft ? <RenewSecretButton onClick={() => onRenew(connection)} /> : null}
        {canReplaceKey(connection) ? <ReplaceKeyButton onClick={() => onReplaceKey(connection)} /> : null}
        <Button size="sm" variant="danger-outline" className="ml-auto" loading={remove.isPending} onClick={() => void askRemove()}>
          <Trash2 aria-hidden />
          {t.connections.remove}
        </Button>
      </div>
    </article>
  )
}

function StoreNeeds() {
  const t = useT()
  return (
    <div className="mt-2 grid w-full max-w-3xl gap-3 text-left sm:grid-cols-2">
      {ALL_STORES.map((store) => {
        const copy = t.connections.stores[channelOf(store).key]
        return (
          <div key={store} className="card grid gap-1.5 p-3.5">
            <StoreTag store={store} />
            <span className="text-[12.5px] text-fg2">{copy.needs}</span>
          </div>
        )
      })}
    </div>
  )
}

/**
 * Store connections (design 14.1-14.12): fingerprints only, add by file or paste, verify, replace an Apple or Google key in
 * place, delete by typing the name.
 */
export function ConnectionsPage() {
  const t = useT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const [adding, setAdding] = useState(false)
  const [renewing, setRenewing] = useState<StoreConnectionDto | null>(null)
  const [editing, setEditing] = useState<StoreConnectionDto | null>(null)
  const [replacing, setReplacing] = useState<StoreConnectionDto | null>(null)
  const allowed = can(OrgPermission.ManageConnections)

  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
    enabled: allowed,
  })

  // Design 14.23: the page needs ManageConnections; which connections exist is not shown to other roles.
  if (!allowed) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={t.states.forbiddenTitle}
        description={t.connections.forbidden}
        action={
          <Link className="btn inv" to={HOME_PATH}>
            {t.states.backToApps}
          </Link>
        }
      />
    )
  }

  const list = connections.data ?? []
  const addButton = (
    <Button variant="primary" onClick={() => setAdding(true)}>
      <Plus aria-hidden />
      {t.connections.add}
    </Button>
  )

  return (
    <>
      <PageHeader title={t.connections.title} subtitle={t.connections.subtitle} actions={list.length > 0 ? addButton : undefined} />
      <MsExpiringBanner
        organizationId={organization.Id}
        enabled={list.some((connection) => connection.Store === StoreKind.Microsoft)}
        onRenew={(connectionId) => setRenewing(list.find((connection) => connection.Id === connectionId) ?? null)}
      />
      {connections.isLoading ? (
        <CardSkeleton />
      ) : connections.isError ? (
        <ScreenState error={connections.error} onRetry={() => void connections.refetch()} />
      ) : list.length === 0 ? (
        <EmptyState icon={<Plug aria-hidden />} title={t.connections.emptyTitle} description={t.connections.emptyBody} action={addButton}>
          <StoreNeeds />
        </EmptyState>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {list.map((connection) => (
            <ConnectionCard
              key={connection.Id}
              connection={connection}
              organizationId={organization.Id}
              onRenew={setRenewing}
              onEdit={setEditing}
              onReplaceKey={setReplacing}
            />
          ))}
        </div>
      )}
      <AddConnectionDialog open={adding} onClose={() => setAdding(false)} organizationId={organization.Id} />
      <EditConnectionDialog
        key={`edit-${editing?.Id ?? 0}`}
        connection={editing}
        organizationId={organization.Id}
        onClose={() => setEditing(null)}
      />
      <ReplaceKeyDialog
        key={`replace-${replacing?.Id ?? 0}`}
        connection={replacing}
        organizationId={organization.Id}
        onClose={() => setReplacing(null)}
      />
      <RenewSecretDialog key={renewing?.Id ?? 0} connection={renewing} organizationId={organization.Id} onClose={() => setRenewing(null)} />
    </>
  )
}
