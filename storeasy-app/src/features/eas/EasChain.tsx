import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Send } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Checkbox, Field, Input } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { easApi } from './api'
import { useEas } from './i18n/useEas'
import { easKeys } from './keys'
import { RELEASE_STATUSES, TRACKS, afterBuildLabel, confirmText, platformLabel, submissionStatusLabel, submissionTone } from './rules'
import { EasAfterBuild, EasPlatform, type EasAppDto, type EasBuildDto, type EasChainDto, type EasSubmissionDto } from './types'

// What happens after an EAS build (docs/eas-builds.md): send to the intake or EAS Submit, and preparing (never approving)
// the review or production plan once the build reached its test channel; the app's defaults and EAS Submit itself.

const AFTER_BUILD: readonly EasAfterBuild[] = [EasAfterBuild.None, EasAfterBuild.Intake, EasAfterBuild.Submit]

/** The after build choice: what happens when the build finished, its Play target and the review plan. */
export function ChainFields({
  value,
  onChange,
  disabled,
}: {
  value: EasChainDto
  onChange: (chain: EasChainDto) => void
  disabled?: boolean
}) {
  const t = useEas()

  return (
    <fieldset className="grid gap-3">
      <legend className="font-semibold">{t.chain.title}</legend>
      <Field label={t.chain.afterBuild}>
        <Select
          aria-label={t.chain.afterBuild}
          value={value.AfterBuild}
          disabled={disabled}
          options={AFTER_BUILD.map((option) => ({ value: option, label: afterBuildLabel(option, t) }))}
          onChange={(afterBuild) => onChange({ ...value, AfterBuild: afterBuild })}
        />
      </Field>
      {value.AfterBuild === EasAfterBuild.Submit ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t.chain.track}>
            <Select
              aria-label={t.chain.track}
              value={value.SubmitTrack ?? 'internal'}
              disabled={disabled}
              options={TRACKS.map((track) => ({ value: track, label: track }))}
              onChange={(track) => onChange({ ...value, SubmitTrack: track })}
            />
          </Field>
          <Field label={t.chain.releaseStatus}>
            <Select
              aria-label={t.chain.releaseStatus}
              value={value.SubmitReleaseStatus ?? 'COMPLETED'}
              disabled={disabled}
              options={RELEASE_STATUSES.map((status) => ({ value: status, label: status }))}
              onChange={(status) => onChange({ ...value, SubmitReleaseStatus: status })}
            />
          </Field>
        </div>
      ) : null}
      <Checkbox
        checked={value.PrepareReviewPlan}
        disabled={disabled}
        onChange={(checked) => onChange({ ...value, PrepareReviewPlan: checked })}
      >
        {t.chain.prepareReviewPlan}
      </Checkbox>
    </fieldset>
  )
}

/** The app's after build defaults, used when a build is started without its own choice. */
export function ChainDefaults({ data, canEdit }: { data: EasAppDto; canEdit: boolean }) {
  const t = useEas()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [chain, setChain] = useState<EasChainDto>(data.Link?.Chain ?? { AfterBuild: EasAfterBuild.None, PrepareReviewPlan: false })

  const save = useMutation({
    mutationFn: () => easApi.saveChain(organization.Id, data.AppId, chain),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.chain.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: easKeys.app(organization.Id, data.AppId) })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  return (
    <section aria-label={t.chain.defaults} className="grid gap-2">
      <h3 className="font-semibold">{t.chain.defaults}</h3>
      <ChainFields value={chain} onChange={setChain} disabled={!canEdit} />
      {canEdit ? (
        <div>
          <Button variant="outline" onClick={() => save.mutate()} loading={save.isPending}>
            {t.chain.saveDefaults}
          </Button>
        </div>
      ) : null}
    </section>
  )
}

/** EAS Submit of one finished build, after the typed confirmation. */
export function SubmitDialog({ appName, build, onClose }: { appName: string; build: EasBuildDto; onClose: () => void }) {
  const t = useEas()
  const base = useT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const queryClient = useQueryClient()
  const [track, setTrack] = useState<string>('internal')
  const [releaseStatus, setReleaseStatus] = useState<string>('COMPLETED')
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)
  const android = build.Platform === EasPlatform.Android
  const expected = confirmText(appName, build.Platform)
  const blocked = android && track === 'production' && !can(OrgPermission.ReleaseProduction)

  const submit = useMutation({
    mutationFn: () =>
      easApi.submit(organization.Id, build.Id, {
        Track: android ? track : null,
        ReleaseStatus: android ? releaseStatus : null,
        Confirm: typed,
      }),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.submit.started, 'success')
      void queryClient.invalidateQueries({ queryKey: easKeys.all })
      onClose()
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const onSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (typed.trim() !== expected) {
      setError(t.start.confirmMismatch)
      return
    }
    setError(null)
    submit.mutate()
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={t.submit.title}
      description={t.submit.intro}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="eas-submit-form" variant="primary" loading={submit.isPending} disabled={blocked}>
            <Send aria-hidden />
            {t.submit.submit}
          </Button>
        </>
      }
    >
      <form id="eas-submit-form" className="grid gap-4" onSubmit={onSubmit} noValidate>
        {error ? <FormErrors title={error} /> : null}
        {android ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t.chain.track}>
              <Select
                aria-label={t.chain.track}
                value={track}
                options={TRACKS.map((value) => ({ value, label: value }))}
                onChange={setTrack}
              />
            </Field>
            <Field label={t.chain.releaseStatus}>
              <Select
                aria-label={t.chain.releaseStatus}
                value={releaseStatus}
                options={RELEASE_STATUSES.map((value) => ({ value, label: value }))}
                onChange={setReleaseStatus}
              />
            </Field>
          </div>
        ) : null}
        {blocked ? <Callout tone="info">{t.submit.production}</Callout> : null}
        <Field label={interpolate(t.submit.confirm, { text: expected })}>
          <Input value={typed} autoComplete="off" onChange={(event) => setTyped(event.target.value)} />
        </Field>
      </form>
    </Modal>
  )
}

/** The EAS Submit submissions of the app. */
export function Submissions({ submissions }: { submissions: EasSubmissionDto[] }) {
  const t = useEas()
  const format = useFormat()

  const columns: Column<EasSubmissionDto>[] = [
    {
      key: 'submission',
      header: t.submissions.submission,
      role: 'main',
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          {platformLabel(row.Platform, t)}
          <Badge small tone={submissionTone(row.Status)}>
            {submissionStatusLabel(row.Status, t)}
          </Badge>
          {row.FromChain ? <span className="text-fg3">{t.submissions.chained}</span> : null}
        </span>
      ),
    },
    {
      key: 'target',
      header: t.submissions.target,
      cell: (row) => (row.Track ? `${row.Track} (${row.ReleaseStatus ?? '-'})` : 'TestFlight'),
    },
    { key: 'started', header: t.submissions.started, cell: (row) => (row.StartedDate ? format.relative(row.StartedDate) : '-') },
    {
      key: 'details',
      header: '',
      role: 'hidePhone',
      cell: (row) => (
        <span className="inline-flex flex-wrap gap-2">
          {row.LogsUrl ? (
            <a
              href={row.LogsUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 underline underline-offset-2"
            >
              {t.submissions.logs}
              <ExternalLink aria-hidden className="size-3.5" />
            </a>
          ) : null}
          {row.ErrorMessage ? <span className="text-danger">{row.ErrorMessage}</span> : null}
        </span>
      ),
    },
  ]

  return (
    <section aria-label={t.submissions.title} className="grid gap-2">
      <h2 className="text-[15px] font-semibold">{t.submissions.title}</h2>
      {submissions.length === 0 ? (
        <p className="text-fg3">{t.submissions.empty}</p>
      ) : (
        <DataTable caption={t.submissions.caption} columns={columns} rows={submissions} rowKey={(row) => row.Id} />
      )}
    </section>
  )
}
