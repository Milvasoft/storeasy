import { useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Hammer, RefreshCw } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Field, Input } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { easApi } from './api'
import { useEas } from './i18n/useEas'
import { easKeys } from './keys'
import {
  canSendToIntake,
  confirmText,
  isActive,
  isSubmissionActive,
  isCancelable,
  isGitRef,
  isProfile,
  platformLabel,
  statusLabel,
  statusTone,
} from './rules'
import { ChainDefaults, ChainFields, SubmitDialog, Submissions } from './EasChain'
import { EasAfterBuild, EasPlatform, type EasAppDto, type EasBuildDto, type EasChainDto } from './types'

// The EAS tab of an app (docs/eas-builds.md): the EAS project link, the start dialog with the typed confirmation and the
// builds (started here or elsewhere) with their progress, links, cancel and the hand off to the build intake.

const PLATFORMS: readonly EasPlatform[] = [EasPlatform.Ios, EasPlatform.Android, EasPlatform.All]

function ExternalAnchor({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 underline underline-offset-2">
      {children}
      <ExternalLink aria-hidden className="size-3.5" />
    </a>
  )
}

function LinkProject({ appId, data, canEdit }: { appId: number; data: EasAppDto; canEdit: boolean }) {
  const t = useEas()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [projectId, setProjectId] = useState<string>('')
  const projects = useQuery({
    queryKey: easKeys.projects(organization.Id, appId),
    queryFn: () => easApi.projects(organization.Id, appId),
    enabled: canEdit && !data.Link,
  })
  const choice = projectId || projects.data?.[0]?.Id || ''

  const link = useMutation({
    mutationFn: (id: string | null) => easApi.link(organization.Id, appId, id),
    meta: { silentError: true },
    onSuccess: (_, id) => {
      showToast(id ? t.link.linked : t.link.unlinked, 'success')
      void queryClient.invalidateQueries({ queryKey: easKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  if (data.Link) {
    return (
      <div className="grid gap-2">
        <p>
          <span className="mono">
            @{data.Link.AccountName}/{data.Link.Slug}
          </span>{' '}
          <ExternalAnchor href={data.Link.ProjectUrl}>{t.link.openProject}</ExternalAnchor>
        </p>
        {data.Link.GitHubRepository ? (
          <p className="text-fg2">
            {t.link.repository}: <span className="mono">{data.Link.GitHubRepository}</span>
          </p>
        ) : (
          <Callout tone="warning">
            {t.link.noRepository} <ExternalAnchor href={data.Link.GitHubSettingsUrl}>{t.link.connectGitHub}</ExternalAnchor>
          </Callout>
        )}
        {canEdit ? (
          <div>
            <Button variant="ghost" onClick={() => link.mutate(null)} loading={link.isPending}>
              {t.link.unlink}
            </Button>
          </div>
        ) : null}
      </div>
    )
  }

  if (!canEdit) {
    return <p className="text-fg3">{t.link.none}</p>
  }

  if (projects.isPending) {
    return <CardSkeleton count={1} />
  }
  if (projects.isError) {
    return <ScreenState error={projects.error} onRetry={() => void projects.refetch()} />
  }
  if (projects.data.length === 0) {
    return <p className="text-fg3">{t.link.noProjects}</p>
  }

  return (
    <div className="flex flex-wrap items-end gap-2">
      <Field label={t.link.choose}>
        <Select
          aria-label={t.link.choose}
          value={choice}
          options={projects.data.map((project) => ({
            value: project.Id,
            label: [
              `@${project.AccountName}/${project.Slug}`,
              project.Match > 0 ? `(${t.link.suggested})` : '',
              project.LinkedAppId ? `(${t.link.linkedElsewhere})` : '',
            ]
              .filter(Boolean)
              .join(' '),
          }))}
          onChange={setProjectId}
        />
      </Field>
      <Button variant="primary" onClick={() => link.mutate(choice)} loading={link.isPending}>
        {t.link.save}
      </Button>
    </div>
  )
}

function StartDialog({ data, onClose }: { data: EasAppDto; onClose: () => void }) {
  const t = useEas()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [platform, setPlatform] = useState<EasPlatform>(EasPlatform.All)
  const [gitRef, setGitRef] = useState(data.Link?.DefaultBranch ?? 'main')
  const [profile, setProfile] = useState('')
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [chain, setChain] = useState<EasChainDto>(data.Link?.Chain ?? { AfterBuild: EasAfterBuild.None, PrepareReviewPlan: false })
  const expected = confirmText(data.AppName, platform)

  const profiles = useQuery({
    queryKey: easKeys.profiles(organization.Id, data.AppId, data.Link?.DefaultBranch ?? ''),
    queryFn: () => easApi.profiles(organization.Id, data.AppId),
  })
  const chosen = profile || profiles.data?.Profiles.find((name) => name === 'production') || profiles.data?.Profiles[0] || ''

  const start = useMutation({
    mutationFn: () =>
      easApi.start(organization.Id, data.AppId, {
        Platform: platform,
        Profile: chosen.trim(),
        GitRef: gitRef.trim(),
        Chain: chain,
        Confirm: typed,
      }),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.start.started, 'success')
      void queryClient.invalidateQueries({ queryKey: easKeys.all })
      onClose()
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!isProfile(chosen)) {
      setError(t.start.invalidProfile)
      return
    }
    if (!isGitRef(gitRef)) {
      setError(t.start.invalidRef)
      return
    }
    if (typed.trim() !== expected) {
      setError(t.start.confirmMismatch)
      return
    }
    setError(null)
    start.mutate()
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={t.start.title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="eas-start-form" variant="primary" loading={start.isPending}>
            <Hammer aria-hidden />
            {t.start.submit}
          </Button>
        </>
      }
    >
      <form id="eas-start-form" className="grid gap-4" onSubmit={submit} noValidate>
        {error ? <FormErrors title={error} /> : null}
        <Field label={t.start.platform}>
          <Select
            aria-label={t.start.platform}
            value={platform}
            options={PLATFORMS.map((value) => ({ value, label: platformLabel(value, t) }))}
            onChange={setPlatform}
          />
        </Field>
        <Field label={t.start.profile} hint={profiles.data?.FromRepository ? t.start.profileHint : t.start.profileDefaults}>
          <Input value={chosen} list="eas-profiles" autoComplete="off" onChange={(event) => setProfile(event.target.value)} />
        </Field>
        <datalist id="eas-profiles">
          {(profiles.data?.Profiles ?? []).map((name) => (
            <option key={name} value={name} />
          ))}
        </datalist>
        <Field label={t.start.gitRef}>
          <Input value={gitRef} mono autoComplete="off" onChange={(event) => setGitRef(event.target.value)} />
        </Field>
        <ChainFields value={chain} onChange={setChain} />
        <p className="text-fg3">{t.start.quota}</p>
        <Field label={interpolate(t.start.confirm, { text: expected })}>
          <Input value={typed} autoComplete="off" onChange={(event) => setTyped(event.target.value)} />
        </Field>
      </form>
    </Modal>
  )
}

function Builds({ data, canRelease }: { data: EasAppDto; canRelease: boolean }) {
  const [submitting, setSubmitting] = useState<EasBuildDto | null>(null)
  const t = useEas()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()

  const done = (message: string) => {
    showToast(message, 'success')
    void queryClient.invalidateQueries({ queryKey: easKeys.all })
  }
  const failed = (failure: unknown) => showToast(apiErrorMessage(failure, base.common.error), 'error')

  const cancel = useMutation({
    mutationFn: (id: number) => easApi.cancel(organization.Id, id),
    meta: { silentError: true },
    onSuccess: () => done(t.builds.canceled),
    onError: failed,
  })
  const intake = useMutation({
    mutationFn: (id: number) => easApi.sendToIntake(organization.Id, id),
    meta: { silentError: true },
    onSuccess: () => done(t.builds.sent),
    onError: failed,
  })

  const askCancel = async (build: EasBuildDto) => {
    if (await confirm({ title: t.builds.cancelTitle, confirmLabel: t.builds.cancel, danger: true })) {
      cancel.mutate(build.Id)
    }
  }

  const columns: Column<EasBuildDto>[] = [
    {
      key: 'build',
      header: t.builds.build,
      role: 'main',
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          {platformLabel(row.Platform, t)}
          <Badge small tone={statusTone(row.Status)}>
            {statusLabel(row.Status, t)}
          </Badge>
          {!row.StartedInStoreasy ? <span className="text-fg3">{t.builds.elsewhere}</span> : null}
        </span>
      ),
    },
    {
      key: 'version',
      header: t.builds.version,
      cell: (row) => <span className="mono">{row.AppVersion ? `${row.AppVersion} (${row.AppBuildVersion ?? '-'})` : '-'}</span>,
    },
    { key: 'profile', header: t.builds.profile, role: 'hidePhone', cell: (row) => row.Profile ?? '-' },
    {
      key: 'ref',
      header: t.builds.ref,
      role: 'hidePhone',
      cell: (row) => <span className="mono">{[row.GitRef, row.GitCommitHash?.slice(0, 7)].filter(Boolean).join(' ') || '-'}</span>,
    },
    { key: 'started', header: t.builds.started, cell: (row) => (row.StartedDate ? format.relative(row.StartedDate) : '-') },
    {
      key: 'links',
      header: '',
      role: 'hidePhone',
      cell: (row) => (
        <span className="inline-flex flex-wrap gap-2">
          {row.PageUrl ? <ExternalAnchor href={row.PageUrl}>{t.builds.page}</ExternalAnchor> : null}
          {row.LogsUrl ? <ExternalAnchor href={row.LogsUrl}>{t.builds.logs}</ExternalAnchor> : null}
          {row.ErrorMessage ? <span className="text-danger">{row.ErrorMessage}</span> : null}
          {row.ChainError ? (
            <span className="text-danger">
              {t.chain.error}: {row.ChainError}
            </span>
          ) : null}
          {row.ReviewPlanIds.map((planId) => (
            <Link key={planId} className="lnk" to={`/activity/plans/${planId}`}>
              {t.chain.plans} #{planId}
            </Link>
          ))}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      role: 'action',
      cell: (row) => {
        if (!canRelease) {
          return null
        }
        if (row.ArtifactSessionId) {
          return (
            <Link className="lnk" to="/intake">
              {t.builds.session}
            </Link>
          )
        }
        if (canSendToIntake(row)) {
          return (
            <span className="inline-flex gap-1">
              <Button variant="ghost" onClick={() => intake.mutate(row.Id)} loading={intake.isPending && intake.variables === row.Id}>
                {t.builds.intake}
              </Button>
              <Button variant="ghost" onClick={() => setSubmitting(row)}>
                {t.submit.open}
              </Button>
            </span>
          )
        }
        return isCancelable(row.Status) && row.BuildId ? (
          <Button variant="ghost" onClick={() => void askCancel(row)}>
            {t.builds.cancel}
          </Button>
        ) : null
      },
    },
  ]

  return (
    <>
      {data.Builds.length === 0 ? (
        <p className="text-fg3">{t.builds.empty}</p>
      ) : (
        <DataTable caption={t.builds.caption} columns={columns} rows={data.Builds} rowKey={(row) => row.Id} />
      )}
      {submitting ? <SubmitDialog appName={data.AppName} build={submitting} onClose={() => setSubmitting(null)} /> : null}
    </>
  )
}

/** The EAS tab of an app (route /apps/:appId/eas). */
export function EasTab({ appId }: { appId: number }) {
  const t = useEas()
  const base = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const queryClient = useQueryClient()
  const canEdit = can(OrgPermission.EditContent)
  const canRelease = can(OrgPermission.ReleaseTest)
  const [starting, setStarting] = useState(false)

  const app = useQuery({
    queryKey: easKeys.app(organization.Id, appId),
    queryFn: () => easApi.app(organization.Id, appId),
    refetchInterval: (query) =>
      (query.state.data?.Builds ?? []).some((build) => isActive(build.Status)) ||
      (query.state.data?.Submissions ?? []).some((submission) => isSubmissionActive(submission.Status))
        ? 20000
        : false,
  })

  const sync = useMutation({
    mutationFn: () => easApi.sync(organization.Id, appId),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.link.syncQueued, 'success')
      window.setTimeout(() => void queryClient.invalidateQueries({ queryKey: easKeys.app(organization.Id, appId) }), 3000)
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  if (app.isPending) {
    return <CardSkeleton count={2} />
  }
  if (app.isError) {
    return <ScreenState error={app.error} onRetry={() => void app.refetch()} />
  }

  const data = app.data

  return (
    <section aria-label={t.title} className="grid gap-4">
      <p className="text-fg2">{t.intro}</p>
      {!data.Connected ? (
        <Callout tone="info">
          {t.notConnected}{' '}
          <Link className="lnk" to="/connections">
            {t.connectionsLink}
          </Link>
        </Callout>
      ) : null}
      <article className="card" aria-label={t.link.title}>
        <div className="card-h">
          <h2 className="h3 mr-auto">{t.link.title}</h2>
          {data.Link && canEdit ? (
            <Button variant="ghost" onClick={() => sync.mutate()} loading={sync.isPending}>
              <RefreshCw aria-hidden />
              {t.link.sync}
            </Button>
          ) : null}
          {data.Link?.GitHubRepository && canRelease ? (
            <Button variant="primary" onClick={() => setStarting(true)}>
              <Hammer aria-hidden />
              {t.start.open}
            </Button>
          ) : null}
        </div>
        <div className="card-b grid gap-2">
          {data.Connected || data.Link ? <LinkProject appId={appId} data={data} canEdit={canEdit && data.Connected} /> : null}
          {data.Link?.SyncedDate ? (
            <p className="text-fg3">{interpolate(t.link.synced, { time: format.relative(data.Link.SyncedDate) })}</p>
          ) : null}
          {data.Link?.SyncError ? <Callout tone="danger">{data.Link.SyncError}</Callout> : null}
          {data.Link && !canRelease ? <p className="text-fg3">{t.start.needsPermission}</p> : null}
          {data.Link ? <ChainDefaults key={JSON.stringify(data.Link.Chain)} data={data} canEdit={canEdit} /> : null}
        </div>
      </article>
      {data.Link ? (
        <section aria-label={t.builds.title} className="grid gap-2">
          <h2 className="text-[15px] font-semibold">{t.builds.title}</h2>
          <Builds data={data} canRelease={canRelease} />
        </section>
      ) : null}
      {data.Link ? <Submissions submissions={data.Submissions ?? []} /> : null}
      {starting ? <StartDialog data={data} onClose={() => setStarting(false)} /> : null}
    </section>
  )
}
