import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Rocket } from 'lucide-react'
import { Badge, Tag } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DatePicker } from '@/components/ui/DatePicker'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input, PageHeader, RadioGroup, Textarea } from '@/components/ui/primitives'
import { BuildProcessingState, OrgPermission, StoreKind } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { buildsApi, releasesApi, type ReleasePlanInput, type RolloutInput } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { PlanDialog } from '@/features/common/PlanView'
import { SubmitDialog } from '@/features/common/SubmitDialog'
import { splitList } from '@/features/common/text'
import {
  ReleasePlanKind,
  ReleaseType,
  RolloutAction,
  type BuildLinkDto,
  type ChangePlanDto,
  type StoreVersionDto,
  type TrackDto,
  type TrackReleaseDto,
} from '@/features/common/types'

// Design 8.2, 8.6-8.13: versions and tracks, every change a plan. Permissions follow ChangePlanRules: release settings,
// build link and export compliance need EditContent; Apple phased release and Play production need ReleaseProduction;
// Play test tracks need ReleaseTest; submission needs SubmitForReview. Buttons the caller cannot use stay, disabled,
// with the reason (design 6.11, 8.10).

export const PRODUCTION = 'production'
// App Store states in which the version still takes changes and can be submitted.
export const EDITABLE_STATES = ['PREPARE_FOR_SUBMISSION', 'DEVELOPER_REJECTED', 'REJECTED', 'METADATA_REJECTED', 'INVALID_BINARY']

export function isProduction(track: string | null | undefined): boolean {
  return (track ?? '').toLowerCase() === PRODUCTION
}

function Reason({ text }: { text: string | null }) {
  return text ? <p className="m-0 text-xs text-fg3">{text}</p> : null
}

type Planner = (run: () => Promise<ChangePlanDto>) => void

// ---- Dialogs --------------------------------------------------------------------------------------------------------------
function LinkBuildDialog({
  context,
  version,
  exportOnly,
  onClose,
  plan,
}: {
  context: WorkspaceContext
  version: StoreVersionDto
  exportOnly: boolean
  onClose: () => void
  plan: Planner
}) {
  const t = useP2()
  const format = useFormat()
  const { organizationId, record } = context
  const [buildId, setBuildId] = useState<number | null>(null)
  const [encryption, setEncryption] = useState<'yes' | 'no' | null>(null)
  const builds = useQuery({
    queryKey: contentKeys.builds(organizationId, record.StoreAppId),
    queryFn: () => buildsApi.list(organizationId, record.StoreAppId, { PageNumber: 1, RowCount: 50 }),
    enabled: !exportOnly,
  })
  const list = builds.data?.Data ?? []
  const chosen = list.find((build) => build.Id === buildId)
  const needsAnswer = exportOnly || (!!chosen && (chosen.UsesNonExemptEncryption === null || chosen.UsesNonExemptEncryption === undefined))
  const ready = (exportOnly || !!chosen) && (!needsAnswer || encryption !== null)
  const label = (build: BuildLinkDto) => `${build.Version ?? '-'} (${build.BuildNumber ?? '-'})`

  const send = () => {
    const answer = encryption === null ? null : encryption === 'yes'
    if (exportOnly) {
      plan(() => buildsApi.planExportCompliance(organizationId, record.StoreAppId, version.VersionId, answer ?? false))
    } else if (chosen) {
      plan(() => buildsApi.planLink(organizationId, record.StoreAppId, version.VersionId, chosen.Id, needsAnswer ? answer : null))
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={interpolate(exportOnly ? t.linkBuild.exportTitle : t.linkBuild.title, { version: version.VersionString ?? '-' })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant="primary" disabled={!ready} onClick={send}>
            {t.plan.make}
          </Button>
        </>
      }
    >
      {!exportOnly ? (
        builds.isLoading ? (
          <CardSkeleton count={1} />
        ) : list.length === 0 ? (
          <p className="text-[13px] text-fg3">{t.linkBuild.noBuilds}</p>
        ) : (
          <>
            <p className="mb-2 text-xs text-fg3">{t.linkBuild.onlyValid}</p>
            <RadioGroup
              name="build"
              label={t.linkBuild.pick}
              value={buildId ?? 0}
              onChange={(value) => setBuildId(value)}
              options={list.map((build) => ({
                value: build.Id,
                label: label(build),
                disabled: build.ProcessingState !== BuildProcessingState.Valid,
                description:
                  build.ProcessingState === BuildProcessingState.Processing
                    ? t.linkBuild.processing
                    : build.ProcessingState !== BuildProcessingState.Valid
                      ? interpolate(t.linkBuild.invalid, { state: String(build.ProcessingState) })
                      : `${format.date(build.UploadedDate)}${build.MinOsVersion ? ` · min ${build.MinOsVersion}` : ''}`,
              }))}
            />
          </>
        )
      ) : null}
      {needsAnswer ? (
        <div className="mt-3">
          {!exportOnly ? <p className="mb-2 text-xs text-fg3">{t.linkBuild.encryptionHint}</p> : null}
          <RadioGroup
            name="encryption"
            label={t.linkBuild.encryption}
            value={encryption ?? ('' as 'yes')}
            onChange={setEncryption}
            options={[
              { value: 'no', label: t.linkBuild.no },
              { value: 'yes', label: t.linkBuild.yes },
            ]}
          />
        </div>
      ) : null}
    </Modal>
  )
}

function ReleaseSettingsDialog({
  version,
  onClose,
  plan,
  context,
}: {
  version: StoreVersionDto
  onClose: () => void
  plan: Planner
  context: WorkspaceContext
}) {
  const t = useP2()
  const [type, setType] = useState<ReleaseType>(
    version.ReleaseType === 'AFTER_APPROVAL'
      ? ReleaseType.AfterApproval
      : version.ReleaseType === 'SCHEDULED'
        ? ReleaseType.Scheduled
        : ReleaseType.Manual,
  )
  const [date, setDate] = useState<string | null>(version.EarliestReleaseDate?.slice(0, 10) ?? null)
  const [phased, setPhased] = useState(!!version.PhasedRelease)
  const [error, setError] = useState<string | null>(null)
  const send = () => {
    if (type === ReleaseType.Scheduled && !date) {
      setError(t.releaseSettings.dateRequired)
      return
    }
    const input: ReleasePlanInput = {
      Kind: ReleasePlanKind.SetReleaseType,
      VersionId: version.VersionId,
      ReleaseType: type,
      EarliestReleaseDate: type === ReleaseType.Scheduled && date ? `${date}T00:00:00Z` : null,
      PhasedRelease: phased,
    }
    plan(() => releasesApi.plan(context.organizationId, context.record.StoreAppId, input))
  }
  return (
    <Modal
      open
      onClose={onClose}
      title={interpolate(t.releaseSettings.title, { version: version.VersionString ?? '-' })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant="primary" onClick={send}>
            {t.plan.make}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <RadioGroup
          name="release-type"
          label={t.releaseSettings.type}
          value={type}
          onChange={setType}
          options={[
            { value: ReleaseType.Manual, label: t.releaseSettings.manual },
            { value: ReleaseType.AfterApproval, label: t.releaseSettings.afterApproval },
            { value: ReleaseType.Scheduled, label: t.releaseSettings.scheduled },
          ]}
        />
        {type === ReleaseType.Scheduled ? (
          <Field label={t.releaseSettings.date} error={error}>
            <DatePicker value={date} onChange={setDate} />
          </Field>
        ) : null}
        <Checkbox checked={phased} onChange={setPhased}>
          {t.releaseSettings.phased}
        </Checkbox>
      </div>
    </Modal>
  )
}

function AssignTrackDialog({
  tracks,
  suggested,
  onClose,
  plan,
  context,
}: {
  tracks: string[]
  suggested: string
  onClose: () => void
  plan: Planner
  context: WorkspaceContext
}) {
  const t = useP2()
  const { can } = context
  const [track, setTrack] = useState(tracks.find((item) => !isProduction(item)) ?? tracks[0] ?? 'internal')
  const [codes, setCodes] = useState(suggested)
  const [name, setName] = useState('')
  const [status, setStatus] = useState<'completed' | 'inProgress' | 'draft'>('completed')
  const [percent, setPercent] = useState('10')
  const [language, setLanguage] = useState('en-US')
  const [notes, setNotes] = useState('')
  const [errors, setErrors] = useState<{ codes?: string; percent?: string; notes?: string }>({})
  const allowed = isProduction(track) ? can(OrgPermission.ReleaseProduction) : can(OrgPermission.ReleaseTest)

  const send = () => {
    const list = splitList(codes)
    const fraction = Number(percent)
    const next = {
      codes: list.length === 0 ? t.assign.codesRequired : undefined,
      percent: status === 'inProgress' && !(fraction > 0 && fraction < 100) ? t.assign.fractionInvalid : undefined,
      notes: notes.length > 500 ? t.assign.notesTooLong : undefined,
    }
    setErrors(next)
    if (next.codes || next.percent || next.notes) {
      return
    }
    plan(() =>
      releasesApi.plan(context.organizationId, context.record.StoreAppId, {
        Kind: ReleasePlanKind.AssignTrack,
        Track: track,
        Name: name.trim() || null,
        VersionCodes: list,
        Status: status,
        UserFraction: status === 'inProgress' ? fraction / 100 : null,
        ReleaseNotes: notes.trim() ? [{ Language: language.trim(), Text: notes.trim() }] : [],
      }),
    )
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={t.assign.title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant="primary" disabled={!allowed} onClick={send}>
            {t.plan.make}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.assign.track}>
          <Select value={track} options={tracks.map((value) => ({ value, label: value }))} onChange={setTrack} />
        </Field>
        {!allowed ? <Reason text={isProduction(track) ? t.release.needsProduction : t.release.needsTest} /> : null}
        {isProduction(track) ? <p className="m-0 text-xs text-fg3">{t.assign.production}</p> : null}
        <Field label={t.assign.codes} hint={t.assign.codesHint} error={errors.codes}>
          <Input mono value={codes} onChange={(event) => setCodes(event.target.value)} />
        </Field>
        <Field label={t.assign.name} optional>
          <Input value={name} onChange={(event) => setName(event.target.value)} />
        </Field>
        <RadioGroup
          name="track-status"
          label={t.assign.status}
          value={status}
          onChange={setStatus}
          options={[
            { value: 'completed', label: t.assign.completed },
            { value: 'inProgress', label: t.assign.inProgress },
            { value: 'draft', label: t.assign.draft },
          ]}
        />
        {status === 'inProgress' ? (
          <Field label={t.assign.fraction} error={errors.percent}>
            <Input inputMode="decimal" value={percent} onChange={(event) => setPercent(event.target.value)} />
          </Field>
        ) : null}
        <div className="grid gap-3 sm:grid-cols-[140px_1fr]">
          <Field label={t.assign.notesLanguage}>
            <Input mono value={language} onChange={(event) => setLanguage(event.target.value)} />
          </Field>
          <Field label={t.assign.notesText} error={errors.notes} counter={{ value: notes.length, max: 500 }} optional>
            <Textarea rows={3} value={notes} onChange={(event) => setNotes(event.target.value)} />
          </Field>
        </div>
      </div>
    </Modal>
  )
}

function RolloutDialog({
  release,
  track,
  onClose,
  plan,
  context,
  halt,
}: {
  release: TrackReleaseDto
  track: string
  onClose: () => void
  plan: Planner
  context: WorkspaceContext
  halt: boolean
}) {
  const t = useP2()
  const current = Math.round((release.UserFraction ?? 0) * 100)
  const [percent, setPercent] = useState(String(Math.min(99, Math.max(current + 1, current < 20 ? 20 : current < 50 ? 50 : current + 1))))
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const name = release.Name ?? (release.VersionCodes ?? []).join(', ')
  const send = () => {
    const input: RolloutInput = halt
      ? { Track: track, Action: RolloutAction.Halt, Reason: reason.trim() || null }
      : { Track: track, Action: RolloutAction.SetPercentage, Percentage: Number(percent) }
    if (!halt) {
      const value = Number(percent)
      if (!(value > current && value < 100)) {
        setError(t.rolloutDialog.percentInvalid)
        return
      }
    }
    plan(() => releasesApi.planRollout(context.organizationId, context.record.StoreAppId, input))
  }
  return (
    <Modal
      open
      onClose={onClose}
      role={halt ? 'alertdialog' : 'dialog'}
      size="sm"
      title={interpolate(halt ? t.release.haltTitle : t.rolloutDialog.title, { name })}
      description={halt ? t.release.haltBody : undefined}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant={halt ? 'danger' : 'primary'} onClick={send}>
            {halt ? t.release.halt : t.plan.make}
          </Button>
        </>
      }
    >
      {halt ? (
        <Field label={t.release.haltReason} optional>
          <Textarea rows={2} value={reason} onChange={(event) => setReason(event.target.value)} />
        </Field>
      ) : (
        <Field label={t.rolloutDialog.percent} error={error} hint={interpolate(t.release.fraction, { percent: current })}>
          <Input inputMode="decimal" value={percent} onChange={(event) => setPercent(event.target.value)} />
        </Field>
      )}
    </Modal>
  )
}

// ---- Apple versions -------------------------------------------------------------------------------------------------------
type AppleDialog = { kind: 'link' | 'export' | 'settings' | 'submit'; version: StoreVersionDto } | null

function AppleVersions({ context, versions, plan }: { context: WorkspaceContext; versions: StoreVersionDto[]; plan: Planner }) {
  const t = useP2()
  const format = useFormat()
  const confirm = useConfirm()
  const { can, editor, app, organizationId, record } = context
  const [dialog, setDialog] = useState<AppleDialog>(null)
  const planAndClose: Planner = (run) => {
    setDialog(null)
    plan(run)
  }
  const production = can(OrgPermission.ReleaseProduction)
  const submitter = can(OrgPermission.SubmitForReview)

  if (versions.length === 0) {
    return <EmptyState icon={<Rocket aria-hidden />} title={t.release.noVersions} />
  }
  const phase = (version: StoreVersionDto, action: RolloutAction) =>
    plan(() => releasesApi.planRollout(organizationId, record.StoreAppId, { VersionId: version.VersionId, Action: action }))

  return (
    <section aria-label={t.release.versions} className="grid gap-3">
      {versions.map((version) => {
        const editable = EDITABLE_STATES.includes(version.State ?? '')
        const phased = version.PhasedRelease
        const paused = phased?.State === 'PAUSED'
        return (
          <article key={version.VersionId} className="card" aria-label={version.VersionString ?? version.VersionId}>
            <div className="card-h">
              <h2 className="h3 mono">{version.VersionString ?? '-'}</h2>
              {version.Platform ? <Tag>{version.Platform}</Tag> : null}
              <Badge tone={editable ? 'info' : 'neutral'}>{version.State ?? '-'}</Badge>
            </div>
            <div className="card-b grid gap-3">
              <dl className="kv">
                <dt>{t.release.build}</dt>
                <dd className="mono">{version.BuildNumber ?? t.release.noBuild}</dd>
                <dt>{t.release.releaseType}</dt>
                <dd>
                  {version.ReleaseType ?? '-'}
                  {version.EarliestReleaseDate
                    ? ` · ${interpolate(t.release.earliest, { date: format.date(version.EarliestReleaseDate) })}`
                    : ''}
                </dd>
                {phased ? (
                  <>
                    <dt>{t.release.phased}</dt>
                    <dd>{interpolate(t.release.phasedDay, { state: phased.State ?? '-', day: phased.CurrentDayNumber ?? 0 })}</dd>
                  </>
                ) : null}
              </dl>
              <div className="flex flex-wrap gap-2">
                {editor ? (
                  <>
                    <Button size="sm" disabled={!editable} onClick={() => setDialog({ kind: 'link', version })}>
                      {t.release.linkBuild}
                    </Button>
                    <Button size="sm" disabled={!editable} onClick={() => setDialog({ kind: 'export', version })}>
                      {t.release.exportCompliance}
                    </Button>
                    <Button size="sm" onClick={() => setDialog({ kind: 'settings', version })}>
                      {t.release.setReleaseType}
                    </Button>
                  </>
                ) : null}
                {phased && phased.State !== 'COMPLETE' ? (
                  <>
                    <Button
                      size="sm"
                      disabled={!production}
                      onClick={() => phase(version, paused ? RolloutAction.Resume : RolloutAction.Pause)}
                    >
                      {paused ? t.release.resume : t.release.pause}
                    </Button>
                    <Button
                      size="sm"
                      variant="danger-outline"
                      disabled={!production}
                      onClick={async () => {
                        const ok = await confirm({
                          title: interpolate(t.release.completeTitle, { version: version.VersionString ?? '-' }),
                          message: t.release.completeBody,
                          confirmLabel: t.release.complete,
                          danger: true,
                        })
                        if (ok) {
                          phase(version, RolloutAction.Complete)
                        }
                      }}
                    >
                      {t.release.complete}
                    </Button>
                  </>
                ) : null}
                <Button
                  size="sm"
                  variant="primary"
                  disabled={!editable || !submitter}
                  onClick={() => setDialog({ kind: 'submit', version })}
                >
                  {t.readiness.submit}
                </Button>
              </div>
              {phased && phased.State !== 'COMPLETE' && !production ? <Reason text={t.release.needsProduction} /> : null}
              {!submitter ? <Reason text={t.release.needsSubmit} /> : null}
            </div>
          </article>
        )
      })}
      {dialog?.kind === 'link' || dialog?.kind === 'export' ? (
        <LinkBuildDialog
          context={context}
          version={dialog.version}
          exportOnly={dialog.kind === 'export'}
          onClose={() => setDialog(null)}
          plan={planAndClose}
        />
      ) : null}
      {dialog?.kind === 'settings' ? (
        <ReleaseSettingsDialog context={context} version={dialog.version} onClose={() => setDialog(null)} plan={planAndClose} />
      ) : null}
      <SubmitDialog
        open={dialog?.kind === 'submit'}
        onClose={() => setDialog(null)}
        organizationId={organizationId}
        storeAppId={record.StoreAppId}
        appName={app.Name}
        versionId={dialog?.version.VersionId ?? null}
        versionString={dialog?.version.VersionString ?? null}
      />
    </section>
  )
}

// ---- Play tracks ----------------------------------------------------------------------------------------------------------
type PlayDialog = { kind: 'assign' } | { kind: 'percent' | 'halt'; release: TrackReleaseDto; track: string } | null

function PlayTracks({ context, tracks, plan }: { context: WorkspaceContext; tracks: TrackDto[]; plan: Planner }) {
  const t = useP2()
  const { can, app, organizationId, record } = context
  const [dialog, setDialog] = useState<PlayDialog>(null)
  const planAndClose: Planner = (run) => {
    setDialog(null)
    plan(run)
  }
  const allowedFor = (track: string) => (isProduction(track) ? can(OrgPermission.ReleaseProduction) : can(OrgPermission.ReleaseTest))
  const newest = (app.Builds ?? []).find((build) => build.StoreAppId === record.StoreAppId)?.BuildNumber ?? ''
  const rollout = (track: string, action: RolloutAction) =>
    plan(() => releasesApi.planRollout(organizationId, record.StoreAppId, { Track: track, Action: action }))

  return (
    <section aria-label={t.release.tracks} className="grid gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="h3 flex-1">{t.release.tracks}</h2>
        {can(OrgPermission.ReleaseTest) ? (
          <Button variant="primary" onClick={() => setDialog({ kind: 'assign' })}>
            {t.release.assign}
          </Button>
        ) : (
          <Reason text={t.release.needsTest} />
        )}
      </div>
      {tracks.length === 0 ? <EmptyState icon={<Rocket aria-hidden />} title={t.release.noTracks} /> : null}
      <div className="grid gap-3 md:grid-cols-2">
        {tracks.map((track) => (
          <article key={track.Track} className="card" aria-label={track.Track}>
            <div className="card-h">
              <h3 className="h3 mono">{track.Track}</h3>
              {(track.GoogleGroups ?? []).length > 0 ? (
                <span className="text-xs text-fg3">{(track.GoogleGroups ?? []).join(', ')}</span>
              ) : null}
            </div>
            <div className="card-b grid gap-3">
              {(track.Releases ?? []).length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.release.noReleases}</p> : null}
              {(track.Releases ?? []).map((release, index) => {
                const allowed = allowedFor(track.Track)
                const percent = release.UserFraction ? Math.round(release.UserFraction * 100) : null
                return (
                  <div key={`${release.Name}-${index}`} className="grid gap-1.5 border-b border-border pb-3 last:border-0 last:pb-0">
                    <div className="flex flex-wrap items-center gap-2 text-[13px]">
                      <b>{release.Name ?? '-'}</b>
                      <span className="mono text-fg2">{(release.VersionCodes ?? []).join(', ')}</span>
                      <Badge small tone={release.Status === 'halted' ? 'danger' : release.Status === 'inProgress' ? 'info' : 'neutral'}>
                        {release.Status ?? '-'}
                      </Badge>
                      {percent !== null ? <span className="mono">{interpolate(t.release.fraction, { percent })}</span> : null}
                      {(release.Countries ?? []).length > 0 ? (
                        <span className="text-xs text-fg3">
                          {interpolate(t.release.countries, { count: (release.Countries ?? []).length })}
                        </span>
                      ) : null}
                    </div>
                    {(release.ReleaseNotes ?? []).map((note) => (
                      <p key={note.Language} className="m-0 text-xs text-fg2">
                        <span className="mono">{note.Language}</span> · {note.Text}
                      </p>
                    ))}
                    {release.Status === 'inProgress' || release.Status === 'halted' ? (
                      <div className="flex flex-wrap gap-2">
                        {release.Status === 'inProgress' ? (
                          <>
                            <Button
                              size="sm"
                              disabled={!allowed}
                              onClick={() => setDialog({ kind: 'percent', release, track: track.Track })}
                            >
                              {t.release.setPercentage}
                            </Button>
                            <Button
                              size="sm"
                              variant="danger-outline"
                              disabled={!allowed}
                              onClick={() => setDialog({ kind: 'halt', release, track: track.Track })}
                            >
                              {t.release.halt}
                            </Button>
                          </>
                        ) : (
                          <Button size="sm" disabled={!allowed} onClick={() => rollout(track.Track, RolloutAction.Resume)}>
                            {t.release.resume}
                          </Button>
                        )}
                        <Button size="sm" disabled={!allowed} onClick={() => rollout(track.Track, RolloutAction.Complete)}>
                          {t.release.complete}
                        </Button>
                      </div>
                    ) : null}
                    {!allowed && (release.Status === 'inProgress' || release.Status === 'halted') ? (
                      <Reason text={isProduction(track.Track) ? t.release.needsProduction : t.release.needsTest} />
                    ) : null}
                  </div>
                )
              })}
            </div>
          </article>
        ))}
      </div>
      {dialog?.kind === 'assign' ? (
        <AssignTrackDialog
          context={context}
          tracks={tracks.length > 0 ? tracks.map((track) => track.Track) : ['internal', 'alpha', 'beta', PRODUCTION]}
          suggested={newest}
          onClose={() => setDialog(null)}
          plan={planAndClose}
        />
      ) : null}
      {dialog?.kind === 'percent' || dialog?.kind === 'halt' ? (
        <RolloutDialog
          context={context}
          release={dialog.release}
          track={dialog.track}
          halt={dialog.kind === 'halt'}
          onClose={() => setDialog(null)}
          plan={planAndClose}
        />
      ) : null}
    </section>
  )
}

function Releases({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const { organizationId, record, store } = context
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const overview = useQuery({
    queryKey: contentKeys.releases(organizationId, record.StoreAppId),
    queryFn: () => releasesApi.overview(organizationId, record.StoreAppId),
  })
  const make = useMutation({ mutationFn: (run: () => Promise<ChangePlanDto>) => run(), onSuccess: setPlan })
  if (overview.isLoading) {
    return <CardSkeleton />
  }
  if (overview.isError || !overview.data) {
    return <ScreenState error={overview.error} onRetry={() => void overview.refetch()} />
  }
  const planner: Planner = (run) => make.mutate(run)
  return (
    <>
      <PageHeader title={t.release.title} />
      {store === StoreKind.Apple ? (
        <AppleVersions context={context} versions={overview.data.Versions ?? []} plan={planner} />
      ) : (
        <PlayTracks context={context} tracks={overview.data.Tracks ?? []} plan={planner} />
      )}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </>
  )
}

export function ReleasesPage() {
  return (
    <AppWorkspace section="release" sub="versions">
      {(context) => <Releases context={context} />}
    </AppWorkspace>
  )
}
