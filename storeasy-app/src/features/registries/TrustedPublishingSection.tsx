import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, ExternalLink } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { CodeBlock } from '@/components/ui/Code'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Field, Input } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import type { RegistriesDictionary } from './i18n/en'
import { registriesKeys } from './keys'
import { checkLabel, checkTone, isSetupRepository, isSetupWorkflow, providerLabel, registryLabel } from './rules'
import { CiProvider, PackageRegistry, type PublisherCheckDto, type SetupInput, type TrustedPublishingDto } from './types'

// The trusted publishing wizard of a package (docs/package-registries.md): the CI provider, repository, workflow and
// environment, then the three steps: what to fill in on the registry, the CI file to copy and the check of the latest
// release. storeasy never writes to the registry or the repository.

const EMPTY: SetupInput = { Provider: null, Repository: '', Workflow: '', Environment: '', RegistryUser: '' }

function CopyButton({ text, t }: { text: string; t: RegistriesDictionary }) {
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      showToast(t.setup.copied, 'success')
    } catch {
      showToast(t.setup.copyFailed, 'warning')
    }
  }
  return (
    <Button variant="outline" onClick={() => void copy()}>
      <Copy aria-hidden />
      {t.setup.copy}
    </Button>
  )
}

function CheckCard({ check, t }: { check: PublisherCheckDto; t: RegistriesDictionary }) {
  return (
    <article className="card" aria-label={t.setup.step3}>
      <div className="card-h flex-wrap">
        <h3 className="h3">{t.setup.step3}</h3>
        <Badge tone={checkTone(check.Status)}>{checkLabel(check.Status, t)}</Badge>
      </div>
      <div className="card-b grid gap-2">
        <p className="text-fg2">{t.setup.step3Body}</p>
        <dl className="grid gap-2 sm:grid-cols-2">
          <div>
            <dt className="text-fg3">{t.setup.expected}</dt>
            <dd className="mono break-all">{[check.ExpectedRepository, check.ExpectedWorkflow].filter(Boolean).join(' ') || '-'}</dd>
          </div>
          <div>
            <dt className="text-fg3">{t.setup.observed}</dt>
            <dd className="mono break-all">
              {[check.Version, check.ObservedRepository, check.ObservedWorkflow].filter(Boolean).join(' ') || '-'}
              {check.CiRunUrl ? (
                <>
                  {' '}
                  <a href={check.CiRunUrl} target="_blank" rel="noopener noreferrer" className="underline">
                    {t.ciRunLink}
                  </a>
                </>
              ) : null}
            </dd>
          </div>
        </dl>
      </div>
    </article>
  )
}

function Steps({ wizard, t }: { wizard: TrustedPublishingDto; t: RegistriesDictionary }) {
  const registry = registryLabel(wizard.Registry)
  return (
    <div className="grid gap-3">
      <article className="card" aria-label={interpolate(t.setup.step1, { registry })}>
        <div className="card-h flex-wrap">
          <h3 className="h3">{interpolate(t.setup.step1, { registry })}</h3>
        </div>
        <div className="card-b grid gap-2">
          <p className="text-fg2">{t.setup.step1Body}</p>
          <dl className="grid gap-2 sm:grid-cols-2">
            {(wizard.RegistryFields ?? []).map((field) => (
              <div key={field.Key}>
                <dt className="text-fg3">{t.setup.field[field.Key as keyof RegistriesDictionary['setup']['field']] ?? field.Key}</dt>
                <dd className="mono break-all">{field.Value ?? '-'}</dd>
              </div>
            ))}
          </dl>
          {wizard.SettingsUrl ? (
            <a href={wizard.SettingsUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 underline">
              {interpolate(t.setup.openSettings, { registry })}
              <ExternalLink aria-hidden className="size-3.5" />
            </a>
          ) : null}
        </div>
      </article>
      {wizard.WorkflowText ? (
        <article className="card" aria-label={t.setup.step2}>
          <div className="card-h flex-wrap">
            <h3 className="h3 mr-auto">{t.setup.step2}</h3>
            <CopyButton text={wizard.WorkflowText} t={t} />
          </div>
          <div className="card-b grid gap-2">
            <p className="text-fg2">{interpolate(t.setup.step2Body, { path: wizard.WorkflowPath ?? '' })}</p>
            <CodeBlock label={wizard.WorkflowPath ?? t.setup.step2}>{wizard.WorkflowText}</CodeBlock>
          </div>
        </article>
      ) : null}
    </div>
  )
}

/** The "Trusted publishing" section of a package page. */
export function TrustedPublishingSection({ packageId, canEdit }: { packageId: number; canEdit: boolean }) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<SetupInput>(EMPTY)
  const [preview, setPreview] = useState<SetupInput | null>(null)
  const [errors, setErrors] = useState<{ repository?: string; workflow?: string }>({})
  const [server, setServer] = useState<string | null>(null)

  const saved = useQuery({
    queryKey: registriesKeys.setup(organization.Id, packageId),
    queryFn: () => registriesApi.setup(organization.Id, packageId),
  })
  const previewed = useQuery({
    queryKey: [...registriesKeys.setup(organization.Id, packageId), 'preview', preview],
    queryFn: () => registriesApi.setup(organization.Id, packageId, preview),
    enabled: preview !== null,
  })

  useEffect(() => {
    const data = saved.data
    if (data?.Saved && data.Provider) {
      setForm({
        Provider: data.Provider,
        Repository: data.Repository ?? '',
        Workflow: data.Workflow ?? '',
        Environment: data.Environment ?? '',
        RegistryUser: data.RegistryUser ?? '',
      })
    } else if (data && form.Provider === null && (data.Providers ?? []).length > 0) {
      setForm({ ...EMPTY, Provider: data.Providers?.[0] ?? null, Workflow: 'release.yml' })
    }
    // Only when the saved setup arrives; edits stay the member's.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [saved.data])

  const save = useMutation({
    mutationFn: (input: SetupInput) => registriesApi.saveSetup(organization.Id, packageId, input),
    meta: { silentError: true },
    onSuccess: (result) => {
      showToast(result.Saved ? t.setup.saved : t.setup.cleared, 'success')
      setPreview(null)
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  if (saved.isPending) {
    return <CardSkeleton count={1} />
  }
  if (saved.isError) {
    return <ScreenState error={saved.error} onRetry={() => void saved.refetch()} />
  }

  const wizard = saved.data
  const providers = wizard.Providers ?? []

  if (providers.length === 0) {
    return (
      <section aria-label={t.setup.title} className="grid gap-2">
        <h2 className="text-[15px] font-semibold">{t.setup.title}</h2>
        <Callout tone="info">{interpolate(t.setup.unsupported, { registry: registryLabel(wizard.Registry) })}</Callout>
      </section>
    )
  }

  const provider = form.Provider ?? providers[0]
  const validate = (): boolean => {
    const next = {
      repository: isSetupRepository(provider, form.Repository) ? undefined : t.setup.repositoryInvalid,
      workflow: isSetupWorkflow(provider, form.Workflow) ? undefined : t.setup.workflowInvalid,
    }
    setErrors(next)
    setServer(null)
    return !next.repository && !next.workflow
  }
  const input: SetupInput = { ...form, Provider: provider }
  const showPreview = (event: FormEvent) => {
    event.preventDefault()
    if (validate()) {
      setPreview(input)
    }
  }
  const shown = preview && previewed.data ? previewed.data : wizard.Saved ? wizard : null

  return (
    <section aria-label={t.setup.title} className="grid gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-[15px] font-semibold">{t.setup.title}</h2>
        {wizard.Check ? <Badge tone={checkTone(wizard.Check.Status)}>{checkLabel(wizard.Check.Status, t)}</Badge> : null}
      </div>
      <p className="text-fg2">{t.setup.intro}</p>
      <form className="card card-b grid gap-3" onSubmit={showPreview} noValidate aria-label={t.setup.title}>
        {server ? <FormErrors title={server} /> : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t.setup.provider}>
            <Select
              aria-label={t.setup.provider}
              value={provider}
              options={providers.map((value) => ({ value, label: providerLabel(value) }))}
              onChange={(value) =>
                setForm({ ...form, Provider: value, Workflow: value === CiProvider.GitLabCi ? '.gitlab-ci.yml' : form.Workflow })
              }
            />
          </Field>
          <Field label={t.setup.repository} hint={t.setup.repositoryHint} error={errors.repository}>
            <Input
              mono
              value={form.Repository}
              autoComplete="off"
              spellCheck={false}
              maxLength={200}
              onChange={(event) => setForm({ ...form, Repository: event.target.value })}
            />
          </Field>
          <Field label={t.setup.workflow} hint={t.setup.workflowHint} error={errors.workflow}>
            <Input
              mono
              value={form.Workflow}
              autoComplete="off"
              spellCheck={false}
              maxLength={200}
              onChange={(event) => setForm({ ...form, Workflow: event.target.value })}
            />
          </Field>
          <Field label={t.setup.environment} hint={t.setup.environmentHint} optional>
            <Input value={form.Environment} maxLength={100} onChange={(event) => setForm({ ...form, Environment: event.target.value })} />
          </Field>
          {wizard.Registry === PackageRegistry.NuGet ? (
            <Field label={t.setup.registryUser} hint={t.setup.registryUserHint} optional>
              <Input
                value={form.RegistryUser}
                maxLength={100}
                onChange={(event) => setForm({ ...form, RegistryUser: event.target.value })}
              />
            </Field>
          ) : null}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="submit" variant="outline" loading={previewed.isFetching}>
            {t.setup.preview}
          </Button>
          {canEdit ? (
            <Button variant="primary" loading={save.isPending} onClick={() => validate() && save.mutate(input)}>
              {t.setup.save}
            </Button>
          ) : null}
          {canEdit && wizard.Saved ? (
            <Button variant="danger-outline" onClick={() => save.mutate(EMPTY)}>
              {t.setup.clear}
            </Button>
          ) : null}
        </div>
      </form>
      {shown ? <Steps wizard={shown} t={t} /> : null}
      {wizard.Saved && wizard.Check ? <CheckCard check={wizard.Check} t={t} /> : null}
    </section>
  )
}
