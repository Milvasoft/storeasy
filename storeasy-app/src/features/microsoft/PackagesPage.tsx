import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/Badge'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { Progress } from '@/components/ui/Progress'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Field, Input, PageHeader } from '@/components/ui/primitives'
import { AssetPurpose, OrgPermission } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { useP2 } from '@/features/common/i18n/useP2'
import { permissionLabel } from '@/features/common/labels'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { percentText, rolloutLabel } from './labels'
import { MsWorkspace, useMsPlan, type MsContext } from './MsWorkspace'
import { MsChangeKind, MsRolloutAction, MsRolloutStatus, type MsContentDto, type MsFlightDto } from './types'

// Design 8.4 and 8.15: the MSIX packages of the app submission or of a package flight, new packages uploaded as assets and
// added through a plan, and the gradual rollout of the published submission (raise, complete, halt). Rollout of the app
// submission needs ReleaseProduction, of a flight ReleaseTest.

export const PACKAGE_EXTENSIONS = ['.msix', '.msixbundle', '.msixupload', '.appx', '.appxbundle', '.appxupload']
const APP = ''

/** A rollout percentage typed by the user: a number 0-100, or null when the field is empty. Undefined when invalid. */
export function parsePercent(value: string): number | null | undefined {
  if (!value.trim()) {
    return null
  }
  const number = Number(value.replace(',', '.'))
  return Number.isFinite(number) && number >= 0 && number <= 100 ? number : undefined
}

function Packages({ context, content, flight }: { context: MsContext; content: MsContentDto; flight: MsFlightDto | null }) {
  const t = useMs()
  const { editable } = context
  const [removed, setRemoved] = useState<string[]>([])
  const [added, setAdded] = useState<Array<{ id: number; name: string }>>([])
  const [rollout, setRollout] = useState('')
  const plan = useMsPlan(context, () => {
    setRemoved([])
    setAdded([])
    setRollout('')
  })
  const packages = content.Packages ?? []
  const percent = parsePercent(rollout)

  return (
    <section className="card" aria-label={t.packages.title}>
      <div className="card-b grid gap-3">
        {plan.element}
        {packages.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.packages.none}</p> : null}
        {packages.length > 0 ? (
          <table className="tbl">
            <thead>
              <tr>
                <th>{t.packages.file}</th>
                <th>{t.packages.version}</th>
                <th>{t.packages.architecture}</th>
                <th aria-hidden />
              </tr>
            </thead>
            <tbody>
              {packages.map((item) => {
                const name = item.FileName ?? ''
                const gone = removed.includes(name)
                return (
                  <tr key={item.Id ?? name}>
                    <td className={cn('mono break-all', gone && 'line-through text-fg3')}>{name}</td>
                    <td className="mono">{item.Version ?? '-'}</td>
                    <td className="mono">{item.Architecture ?? '-'}</td>
                    <td className="text-right">
                      {editable ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          aria-label={`${gone ? t.packages.undo : t.packages.remove}: ${name}`}
                          onClick={() => setRemoved((list) => (gone ? list.filter((entry) => entry !== name) : [...list, name]))}
                        >
                          {gone ? t.packages.undo : t.packages.remove}
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        ) : null}
        {added.map((item) => (
          <div key={item.id} className="flex items-center gap-2 text-[13px]">
            <Badge small tone="success">
              {interpolate(t.packages.uploaded, { name: item.name })}
            </Badge>
            <Button
              size="sm"
              variant="ghost"
              aria-label={`${t.packages.remove}: ${item.name}`}
              onClick={() => setAdded((list) => list.filter((entry) => entry.id !== item.id))}
            >
              {t.packages.remove}
            </Button>
          </div>
        ))}
        {editable ? (
          <>
            <AssetUpload
              key={added.length}
              purpose={AssetPurpose.BuildArtifact}
              accept={PACKAGE_EXTENSIONS}
              label={t.packages.upload}
              hint={t.packages.uploadHint}
              onUploaded={(asset) => setAdded((list) => [...list, { id: asset.Id, name: asset.FileName }])}
            />
            {!flight ? (
              <Field
                label={t.packages.rolloutLabel}
                hint={t.packages.rolloutHint}
                error={percent === undefined ? t.packages.rolloutInvalid : null}
              >
                <Input inputMode="decimal" value={rollout} onChange={(event) => setRollout(event.target.value)} />
              </Field>
            ) : null}
            {added.length > 0 || removed.length > 0 ? (
              <div>
                <Button
                  variant="primary"
                  disabled={percent === undefined}
                  loading={plan.pending}
                  onClick={() =>
                    plan.make({
                      Kind: MsChangeKind.Packages,
                      FlightId: flight?.FlightId ?? null,
                      Packages: {
                        AddAssetIds: added.map((item) => item.id),
                        RemoveFileNames: removed,
                        RolloutPercentage: flight ? null : (percent ?? null),
                      },
                    })
                  }
                >
                  {t.planMake}
                </Button>
              </div>
            ) : null}
          </>
        ) : null}
      </div>
    </section>
  )
}

function Rollout({ context, content, flight }: { context: MsContext; content: MsContentDto; flight: MsFlightDto | null }) {
  const t = useMs()
  const p2 = useP2()
  const { product, editor, can } = context
  const [value, setValue] = useState('')
  const plan = useMsPlan(context, () => setValue(''))
  const rollout = content.Rollout
  const submissionId = flight ? flight.LastPublishedSubmissionId : product.LastPublishedSubmissionId
  const permission = flight ? OrgPermission.ReleaseTest : OrgPermission.ReleaseProduction
  const allowed = can(permission)
  const percent = parsePercent(value)
  const live = rollout?.IsPackageRollout && rollout.Status !== MsRolloutStatus.Complete && rollout.Status !== MsRolloutStatus.Stopped

  const change = (action: MsRolloutAction, percentage: number | null = null) =>
    plan.make({
      Kind: MsChangeKind.Rollout,
      FlightId: flight?.FlightId ?? null,
      Rollout: { SubmissionId: submissionId ?? '', Action: action, Percentage: percentage },
    })

  return (
    <section className="card" aria-label={t.packages.rollout}>
      <div className="card-h">
        <h2 className="h3 flex-1">{t.packages.rollout}</h2>
        {rollout?.IsPackageRollout ? <Badge tone="info">{rolloutLabel(rollout.Status, t)}</Badge> : null}
      </div>
      <div className="card-b grid gap-3">
        {plan.element}
        {!rollout?.IsPackageRollout || !submissionId ? (
          <p className="m-0 text-[13px] text-fg3">{t.packages.noRollout}</p>
        ) : (
          <>
            <p className="m-0 text-[13px] text-fg2">{t.packages.rolloutLead}</p>
            <div className="grid gap-1">
              <b className="text-2xl">{percentText(rollout.Percentage, t)}</b>
              <Progress value={rollout.Percentage} max={100} label={t.packages.rollout} />
            </div>
            <dl className="kv text-[13px]">
              <dt>{t.packages.submission}</dt>
              <dd className="mono">{submissionId}</dd>
              <dt>{t.packages.fallback}</dt>
              <dd className="mono">{rollout.FallbackSubmissionId ?? '-'}</dd>
            </dl>
            {editor && live ? (
              <div className="grid gap-2">
                <Field
                  label={t.packages.newPercent}
                  hint={interpolate(t.packages.current, { percent: percentText(rollout.Percentage, t) })}
                  error={percent === undefined ? t.packages.rolloutInvalid : null}
                >
                  <Input inputMode="decimal" value={value} disabled={!allowed} onChange={(event) => setValue(event.target.value)} />
                </Field>
                <div className="flex flex-wrap gap-2">
                  <Button variant="danger-outline" disabled={!allowed} loading={plan.pending} onClick={() => change(MsRolloutAction.Halt)}>
                    {t.packages.halt}
                  </Button>
                  <Button disabled={!allowed} loading={plan.pending} onClick={() => change(MsRolloutAction.Complete)}>
                    {t.packages.complete}
                  </Button>
                  <Button
                    variant="primary"
                    disabled={!allowed || percent === undefined || percent === null}
                    loading={plan.pending}
                    onClick={() => change(MsRolloutAction.SetPercentage, percent ?? null)}
                  >
                    {interpolate(t.packages.setPercent, { percent: percentText(percent ?? null, t) })}
                  </Button>
                </div>
                <p className="m-0 text-xs text-fg3">{t.packages.haltNote}</p>
                {!allowed ? (
                  <p className="m-0 text-xs text-fg3">{interpolate(t.packages.needs, { permission: permissionLabel(permission, p2) })}</p>
                ) : null}
              </div>
            ) : null}
          </>
        )}
      </div>
    </section>
  )
}

function PackagesScreen({ context }: { context: MsContext }) {
  const t = useMs()
  const { organizationId, record } = context
  const [target, setTarget] = useState<string>(APP)
  const flights = useQuery({
    queryKey: msKeys.flights(organizationId, record.StoreAppId),
    queryFn: () => msApi.flights(organizationId, record.StoreAppId),
  })
  const flight = (flights.data ?? []).find((item) => item.FlightId === target) ?? null
  const content = useQuery({
    queryKey: msKeys.content(organizationId, record.StoreAppId, flight?.FlightId ?? null, null),
    queryFn: () => msApi.content(organizationId, record.StoreAppId, { FlightId: flight?.FlightId ?? null }),
  })

  return (
    <>
      <PageHeader title={t.packages.title} subtitle={t.packages.lead} />
      <div className="mb-3 max-w-sm">
        <Field label={t.packages.target}>
          <Select
            aria-label={t.packages.target}
            value={target}
            options={[
              { value: APP, label: t.packages.app },
              ...(flights.data ?? []).map((item) => ({
                value: item.FlightId,
                label: interpolate(t.packages.flight, { name: item.FriendlyName ?? item.FlightId }),
              })),
            ]}
            onChange={setTarget}
          />
        </Field>
      </div>
      {content.isLoading ? (
        <CardSkeleton />
      ) : content.isError || !content.data ? (
        <ScreenState error={content.error} onRetry={() => void content.refetch()} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-[1fr_380px]">
          <Packages key={target} context={context} content={content.data} flight={flight} />
          <Rollout key={`rollout-${target}`} context={context} content={content.data} flight={flight} />
        </div>
      )}
    </>
  )
}

export function MsPackagesPage() {
  return <MsWorkspace sub="packages">{(context) => <PackagesScreen context={context} />}</MsWorkspace>
}
