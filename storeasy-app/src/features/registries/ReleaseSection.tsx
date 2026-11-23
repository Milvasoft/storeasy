import { useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Plus, Rocket, Search, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, Checkbox, Field, Input, TableSkeleton } from '@/components/ui/primitives'
import { ChangePlanStatus } from '@/features/common/types'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import type { RegistriesDictionary } from './i18n/en'
import { registriesKeys } from './keys'
import {
  EMPTY_RELEASE,
  cleanRelease,
  confirmationMatches,
  followLabel,
  followTone,
  formatLabel,
  planKindLabel,
  triggerHint,
  triggerLabel,
} from './rules'
import {
  RegistryPlanKind,
  ReleaseFollowStatus,
  ReleaseTrigger,
  VersionFileFormat,
  type RegistryPlanDto,
  type ReleaseScope,
  type ReleaseSettings,
  type ReleaseSettingsDto,
} from './types'

// Releases through the repository's own CI (docs/package-registries.md, Releases): the settings of a package or a group,
// the plan of a version with its exact changes, the typed approval and the progress of started releases. storeasy writes
// nothing to GitHub or a registry until a plan is approved.

const TRIGGERS: readonly ReleaseTrigger[] = [
  ReleaseTrigger.VersionBump,
  ReleaseTrigger.Tag,
  ReleaseTrigger.VersionBumpAndTag,
  ReleaseTrigger.Dispatch,
]

const FORMATS: readonly VersionFileFormat[] = [
  VersionFileFormat.MsBuild,
  VersionFileFormat.PackageJson,
  VersionFileFormat.Toml,
  VersionFileFormat.Ruby,
  VersionFileFormat.Pom,
  VersionFileFormat.PlainText,
]

export function scopeKey(scope: ReleaseScope): string {
  return scope.PackageGroupId ? `group-${scope.PackageGroupId}` : `package-${scope.TrackedPackageId}`
}

function ExternalAnchor({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 underline underline-offset-2">
      {children}
      <ExternalLink aria-hidden className="size-3.5" />
    </a>
  )
}

function stateLabel(status: ChangePlanStatus, t: RegistriesDictionary): string {
  const name = Object.keys(ChangePlanStatus).find((key) => ChangePlanStatus[key as keyof typeof ChangePlanStatus] === status) ?? ''
  return t.plan.stateLabel[(name.charAt(0).toLowerCase() + name.slice(1)) as keyof RegistriesDictionary['plan']['stateLabel']] ?? name
}

function problemLabel(key: string, t: RegistriesDictionary): string {
  return t.release.problems[key as keyof RegistriesDictionary['release']['problems']] ?? key
}

/** The plan of a release or a registry operation, with the typed confirmation that approves it. */
export function RegistryPlanDialog({ plan, scope, onClose }: { plan: RegistryPlanDto; scope: ReleaseScope; onClose: () => void }) {
  const t = useRegistries()
  const base = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const queryClient = useQueryClient()
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)
  const canApprove = can(OrgPermission.ReleaseProduction)
  const pending = plan.Status === ChangePlanStatus.Pending

  const done = (message: string) => {
    showToast(message, 'success')
    void queryClient.invalidateQueries({ queryKey: registriesKeys.plans(organization.Id, scopeKey(scope)) })
    void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
    onClose()
  }

  const approve = useMutation({
    mutationFn: () => registriesApi.approvePlan(organization.Id, plan.Id, typed),
    meta: { silentError: true },
    onSuccess: () => done(t.plan.approved),
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const reject = useMutation({
    mutationFn: () => registriesApi.rejectPlan(organization.Id, plan.Id),
    meta: { silentError: true },
    onSuccess: () => done(t.plan.rejected),
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!confirmationMatches(plan.ConfirmText, typed)) {
      setError(t.plan.confirmMismatch)
      return
    }
    setError(null)
    approve.mutate()
  }

  const inputs = Object.entries(plan.DispatchInputs ?? {})

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={interpolate(t.plan.title, { title: plan.Title })}
      description={t.plan.intro}
      footer={
        pending ? (
          <>
            <Button variant="ghost" onClick={() => reject.mutate()} loading={reject.isPending} disabled={!canApprove}>
              {t.plan.reject}
            </Button>
            <Button type="submit" form="registry-plan-form" variant="primary" loading={approve.isPending} disabled={!canApprove}>
              <Rocket aria-hidden />
              {t.plan.approve}
            </Button>
          </>
        ) : (
          <Button variant="ghost" onClick={onClose}>
            {base.common.close}
          </Button>
        )
      }
    >
      <form id="registry-plan-form" className="grid gap-4" onSubmit={submit} noValidate>
        {error ? <FormErrors title={error} /> : null}
        {plan.Files.length > 0 ? (
          <section aria-label={t.plan.files} className="grid gap-2">
            <h3 className="font-semibold">{t.plan.files}</h3>
            {plan.Files.map((file) => (
              <div key={file.Path} className="card card-b grid gap-1">
                <span className="mono">
                  {file.Path} <span className="text-fg3">{interpolate(t.plan.line, { line: file.Line })}</span>
                </span>
                <pre className="mono overflow-x-auto text-[13px]" aria-label={file.Path}>
                  <span className="block text-danger">- {file.Before}</span>
                  <span className="block text-success">+ {file.After}</span>
                </pre>
              </div>
            ))}
          </section>
        ) : null}
        {plan.Tag ? (
          <p>
            {t.plan.tag}: <span className="mono">{plan.Tag}</span>
          </p>
        ) : null}
        {plan.Kind === RegistryPlanKind.Release && plan.Release?.Trigger === ReleaseTrigger.Dispatch ? (
          <div>
            <p>
              {t.plan.dispatch}: <span className="mono">{plan.Release.Workflow}</span>
            </p>
            {inputs.length > 0 ? (
              <p className="text-fg2">
                {t.plan.inputs}: <span className="mono">{inputs.map(([key, value]) => `${key}=${value}`).join(', ')}</span>
              </p>
            ) : null}
          </div>
        ) : null}
        {plan.Kind !== RegistryPlanKind.Release ? (
          <p>
            {t.plan.operation}: {planKindLabel(plan.Kind, t)} <span className="mono">{plan.Version}</span>
            {plan.Argument ? (
              <>
                {' '}
                ({t.plan.argument}: <span className="mono">{plan.Argument}</span>)
              </>
            ) : null}
          </p>
        ) : null}
        {pending ? (
          <>
            <p className="text-fg3">{interpolate(t.plan.expires, { date: format.dateTime(plan.ExpiryDate) })}</p>
            {!canApprove ? <Callout tone="info">{t.plan.needsPermission}</Callout> : null}
            <Field label={interpolate(t.plan.confirm, { text: plan.ConfirmText })}>
              <Input value={typed} autoComplete="off" disabled={!canApprove} onChange={(event) => setTyped(event.target.value)} />
            </Field>
          </>
        ) : null}
        {plan.Error ? <Callout tone="danger">{plan.Error}</Callout> : null}
      </form>
    </Modal>
  )
}

/** The plans of a package or a group and the progress of started releases. */
export function RegistryPlans({ scope }: { scope: ReleaseScope }) {
  const t = useRegistries()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const [open, setOpen] = useState<RegistryPlanDto | null>(null)

  const plans = useQuery({
    queryKey: registriesKeys.plans(organization.Id, scopeKey(scope)),
    queryFn: () => registriesApi.plans(organization.Id, scope),
    refetchInterval: (query) =>
      (query.state.data ?? []).some(
        (plan) => plan.Status === ChangePlanStatus.Approved || plan.FollowStatus === ReleaseFollowStatus.Following,
      )
        ? 15000
        : false,
  })

  const columns: Column<RegistryPlanDto>[] = [
    { key: 'title', header: t.plan.history, role: 'main', cell: (row) => row.Title },
    {
      key: 'status',
      header: t.plan.status,
      cell: (row) => (
        <Badge
          small
          tone={row.Status === ChangePlanStatus.Failed ? 'danger' : row.Status === ChangePlanStatus.Pending ? 'warning' : 'neutral'}
        >
          {stateLabel(row.Status, t)}
        </Badge>
      ),
    },
    {
      key: 'progress',
      header: t.plan.progress,
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          {row.FollowStatus ? (
            <Badge small tone={followTone(row.FollowStatus)}>
              {followLabel(row.FollowStatus, t)}
            </Badge>
          ) : null}
          {row.RunUrl ? <ExternalAnchor href={row.RunUrl}>{t.plan.run}</ExternalAnchor> : null}
          {!row.FollowStatus && !row.RunUrl ? '-' : null}
        </span>
      ),
    },
    { key: 'created', header: t.plan.created, role: 'hidePhone', cell: (row) => (row.CreationDate ? format.date(row.CreationDate) : '-') },
    {
      key: 'open',
      header: '',
      role: 'action',
      cell: (row) => (
        <Button variant="ghost" onClick={() => setOpen(row)}>
          {t.plan.open}
        </Button>
      ),
    },
  ]

  let body
  if (plans.isPending) {
    body = <TableSkeleton columns={[t.plan.history, t.plan.status, t.plan.progress]} rows={3} label={t.plan.history} />
  } else if (plans.isError) {
    body = <ScreenState error={plans.error} onRetry={() => void plans.refetch()} />
  } else if (plans.data.length === 0) {
    body = <p className="text-fg3">{t.plan.empty}</p>
  } else {
    body = <DataTable caption={t.plan.history} columns={columns} rows={plans.data} rowKey={(row) => row.Id} />
  }

  return (
    <section aria-label={t.plan.history} className="grid gap-2">
      <h3 className="font-semibold">{t.plan.history}</h3>
      {body}
      {open ? <RegistryPlanDialog plan={open} scope={scope} onClose={() => setOpen(null)} /> : null}
    </section>
  )
}

function SettingsForm({ scope, data, canEdit }: { scope: ReleaseScope; data: ReleaseSettingsDto; canEdit: boolean }) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<ReleaseSettings>(data.Settings ?? EMPTY_RELEASE)
  const [server, setServer] = useState<string | null>(null)
  const [detecting, setDetecting] = useState(false)
  const bumps = form.Trigger === ReleaseTrigger.VersionBump || form.Trigger === ReleaseTrigger.VersionBumpAndTag
  const tags = form.Trigger === ReleaseTrigger.Tag || form.Trigger === ReleaseTrigger.VersionBumpAndTag
  const dispatch = form.Trigger === ReleaseTrigger.Dispatch

  const detect = useQuery({
    queryKey: [...registriesKeys.release(organization.Id, scopeKey(scope)), 'detect'],
    queryFn: () => registriesApi.detectRelease(organization.Id, scope, form.Repository.trim() || undefined),
    enabled: detecting,
    retry: false,
  })

  const save = useMutation({
    mutationFn: (settings: ReleaseSettings | null) => registriesApi.saveRelease(organization.Id, scope, settings),
    meta: { silentError: true },
    onSuccess: (_, settings) => {
      showToast(settings ? t.release.saved : t.release.cleared, 'success')
      void queryClient.invalidateQueries({ queryKey: registriesKeys.release(organization.Id, scopeKey(scope)) })
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setServer(null)
    save.mutate(cleanRelease(form))
  }

  const setFile = (index: number, change: Partial<ReleaseSettings['VersionFiles'][number]>) =>
    setForm({ ...form, VersionFiles: form.VersionFiles.map((file, i) => (i === index ? { ...file, ...change } : file)) })

  return (
    <form className="grid gap-3" onSubmit={submit} noValidate aria-label={t.release.title}>
      {server ? <FormErrors title={server} /> : null}
      {data.Problems.length > 0 && data.Settings ? (
        <Callout tone="warning">
          <ul className="list-disc pl-5">
            {data.Problems.map((problem) => (
              <li key={problem}>{problemLabel(problem, t)}</li>
            ))}
          </ul>
        </Callout>
      ) : null}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t.release.repository} hint={t.release.repositoryHint}>
          <Input
            value={form.Repository}
            disabled={!canEdit}
            autoComplete="off"
            onChange={(event) => setForm({ ...form, Repository: event.target.value })}
          />
        </Field>
        <Field label={t.release.branch}>
          <Input
            value={form.Branch}
            disabled={!canEdit}
            autoComplete="off"
            onChange={(event) => setForm({ ...form, Branch: event.target.value })}
          />
        </Field>
      </div>
      <Field label={t.release.trigger} hint={triggerHint(form.Trigger, t)}>
        <Select
          aria-label={t.release.trigger}
          value={form.Trigger}
          disabled={!canEdit}
          options={TRIGGERS.map((trigger) => ({ value: trigger, label: triggerLabel(trigger, t) }))}
          onChange={(trigger) => setForm({ ...form, Trigger: trigger })}
        />
      </Field>
      {bumps ? (
        <fieldset className="grid gap-2">
          <legend className="font-semibold">{t.release.versionFiles}</legend>
          {form.VersionFiles.map((file, index) => (
            <div key={index} className="flex flex-wrap items-end gap-2">
              <Field label={t.release.versionFile}>
                <Input
                  value={file.Path}
                  disabled={!canEdit}
                  autoComplete="off"
                  onChange={(event) => setFile(index, { Path: event.target.value })}
                />
              </Field>
              <Field label={t.release.format}>
                <Select
                  aria-label={`${t.release.format} ${index + 1}`}
                  value={file.Format}
                  disabled={!canEdit}
                  options={FORMATS.map((value) => ({ value, label: formatLabel(value, t) }))}
                  onChange={(value) => setFile(index, { Format: value })}
                />
              </Field>
              {canEdit ? (
                <Button
                  variant="ghost"
                  aria-label={t.release.removeFile}
                  onClick={() => setForm({ ...form, VersionFiles: form.VersionFiles.filter((_, i) => i !== index) })}
                >
                  <Trash2 aria-hidden />
                </Button>
              ) : null}
            </div>
          ))}
          {canEdit ? (
            <div>
              <Button
                variant="ghost"
                onClick={() => setForm({ ...form, VersionFiles: [...form.VersionFiles, { Path: '', Format: VersionFileFormat.MsBuild }] })}
              >
                <Plus aria-hidden />
                {t.release.addFile}
              </Button>
            </div>
          ) : null}
        </fieldset>
      ) : null}
      {tags ? (
        <Field label={t.release.tagPattern} hint={t.release.tagPatternHint}>
          <Input
            value={form.TagPattern ?? ''}
            disabled={!canEdit}
            autoComplete="off"
            onChange={(event) => setForm({ ...form, TagPattern: event.target.value })}
          />
        </Field>
      ) : null}
      <Field label={t.release.workflow} hint={t.release.workflowHint} optional={!dispatch}>
        <Input
          value={form.Workflow ?? ''}
          disabled={!canEdit}
          autoComplete="off"
          onChange={(event) => setForm({ ...form, Workflow: event.target.value })}
        />
      </Field>
      {dispatch ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t.release.versionInput}>
            <Input
              value={form.DispatchVersionInput ?? ''}
              disabled={!canEdit}
              autoComplete="off"
              onChange={(event) => setForm({ ...form, DispatchVersionInput: event.target.value })}
            />
          </Field>
          <Field label={t.release.prereleaseInput} optional>
            <Input
              value={form.DispatchPrereleaseInput ?? ''}
              disabled={!canEdit}
              autoComplete="off"
              onChange={(event) => setForm({ ...form, DispatchPrereleaseInput: event.target.value })}
            />
          </Field>
          <Checkbox
            checked={form.DispatchTakesTag}
            disabled={!canEdit}
            onChange={(checked) => setForm({ ...form, DispatchTakesTag: checked })}
          >
            {t.release.takesTag}
          </Checkbox>
        </div>
      ) : null}
      {canEdit ? (
        <div className="flex flex-wrap gap-2">
          <Button type="submit" variant="primary" loading={save.isPending}>
            {t.release.save}
          </Button>
          <Button
            variant="ghost"
            onClick={() => (detecting ? void detect.refetch() : setDetecting(true))}
            loading={detect.isFetching}
            disabled={!data.GitHubConnected}
          >
            <Search aria-hidden />
            {t.release.detect}
          </Button>
          {data.Settings && !data.FromGroup ? (
            <Button variant="ghost" onClick={() => save.mutate(null)}>
              {t.release.clear}
            </Button>
          ) : null}
        </div>
      ) : null}
      {detecting && detect.isError ? <FormErrors title={apiErrorMessage(detect.error, base.common.error)} /> : null}
      {detecting && detect.data ? (
        <section aria-label={t.release.suggestions} className="grid gap-2">
          <h4 className="font-semibold">{t.release.suggestions}</h4>
          {detect.data.length === 0 ? <p className="text-fg3">{t.release.noSuggestions}</p> : null}
          {detect.data.map((suggestion, index) => (
            <div key={index} className="card card-b flex flex-wrap items-center gap-2">
              <span className="mr-auto">
                <Badge small tone="info">
                  {triggerLabel(suggestion.Settings.Trigger, t)}
                </Badge>{' '}
                {suggestion.Reason}
              </span>
              <Button variant="outline" onClick={() => setForm({ ...EMPTY_RELEASE, ...suggestion.Settings })}>
                {t.release.use}
              </Button>
            </div>
          ))}
        </section>
      ) : null}
    </form>
  )
}

function PlanRelease({ scope, data }: { scope: ReleaseScope; data: ReleaseSettingsDto }) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [prerelease, setPrerelease] = useState('')
  const [version, setVersion] = useState(data.NextVersions[0]?.Version ?? '')
  const [error, setError] = useState<string | null>(null)
  const [plan, setPlan] = useState<RegistryPlanDto | null>(null)

  const suggestions = useQuery({
    queryKey: [...registriesKeys.release(organization.Id, scopeKey(scope)), prerelease.trim()],
    queryFn: () => registriesApi.release(organization.Id, scope, prerelease.trim()),
    enabled: prerelease.trim().length > 0,
  })
  const next = (prerelease.trim() ? suggestions.data?.NextVersions : null) ?? data.NextVersions

  const create = useMutation({
    mutationFn: () => registriesApi.planRelease(organization.Id, scope, version.trim()),
    meta: { silentError: true },
    onSuccess: (created) => {
      setPlan(created)
      void queryClient.invalidateQueries({ queryKey: registriesKeys.plans(organization.Id, scopeKey(scope)) })
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!version.trim()) {
      setError(t.release.versionRequired)
      return
    }
    setError(null)
    create.mutate()
  }

  return (
    <form className="grid gap-3" onSubmit={submit} noValidate aria-label={t.release.plan}>
      {error ? <FormErrors title={error} /> : null}
      <p>
        {t.release.current}: <span className="mono">{data.CurrentVersion ?? '-'}</span>
      </p>
      <div className="flex flex-wrap gap-2" role="group" aria-label={t.release.next}>
        {next.map((suggestion) => (
          <Button
            key={suggestion.Kind}
            variant={suggestion.Version === version ? 'primary' : 'outline'}
            onClick={() => setVersion(suggestion.Version)}
          >
            {t.release.suggestion[suggestion.Kind as keyof RegistriesDictionary['release']['suggestion']] ?? suggestion.Kind}{' '}
            <span className="mono">{suggestion.Version}</span>
          </Button>
        ))}
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t.release.custom}>
          <Input value={version} mono autoComplete="off" onChange={(event) => setVersion(event.target.value)} />
        </Field>
        <Field label={t.release.prerelease} hint={t.release.prereleaseHint} optional>
          <Input value={prerelease} autoComplete="off" onChange={(event) => setPrerelease(event.target.value)} />
        </Field>
      </div>
      <div>
        <Button type="submit" variant="primary" loading={create.isPending}>
          <Rocket aria-hidden />
          {t.release.plan}
        </Button>
      </div>
      {plan ? <RegistryPlanDialog plan={plan} scope={scope} onClose={() => setPlan(null)} /> : null}
    </form>
  )
}

/** The release tab of a package, or the release part of a group card. */
export function ReleaseSection({ scope, canEdit }: { scope: ReleaseScope; canEdit: boolean }) {
  const t = useRegistries()
  const organization = useCurrentOrganization()

  const release = useQuery({
    queryKey: registriesKeys.release(organization.Id, scopeKey(scope)),
    queryFn: () => registriesApi.release(organization.Id, scope),
  })

  if (release.isPending) {
    return <TableSkeleton columns={[t.release.repository, t.release.trigger]} rows={2} label={t.release.title} />
  }
  if (release.isError) {
    return <ScreenState error={release.error} onRetry={() => void release.refetch()} />
  }

  const data = release.data
  const ready = data.Settings != null && data.Problems.length === 0

  return (
    <section aria-label={t.release.title} className="grid gap-4">
      <p className="text-fg2">{t.release.intro}</p>
      {!data.GitHubConnected ? <Callout tone="info">{t.release.githubMissing}</Callout> : null}
      {data.FromGroup && data.PackageGroupName ? (
        <Callout tone="info">{interpolate(t.release.fromGroup, { name: data.PackageGroupName })}</Callout>
      ) : null}
      {!data.Settings ? <p className="text-fg3">{t.release.notSet}</p> : null}
      <SettingsForm key={JSON.stringify(data.Settings)} scope={scope} data={data} canEdit={canEdit && !data.FromGroup} />
      {ready && canEdit && data.GitHubConnected ? <PlanRelease scope={scope} data={data} /> : null}
      <RegistryPlans scope={scope} />
    </section>
  )
}
