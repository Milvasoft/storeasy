import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink } from 'lucide-react'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import { registriesKeys } from './keys'
import { RegistryPlanDialog, scopeKey } from './ReleaseSection'
import { connectionKindLabel, keyKindOf, looksLikeToken, needsArgument, operationsOf, planKindLabel, pypiManageUrl } from './rules'
import { PackageRegistry, RegistryPlanKind, type RegistryPlanDto } from './types'

// Registry actions on one version (docs/package-registries.md, Registry operations): NuGet unlist and relist, npm deprecate,
// undeprecate and dist-tags, crates.io yank and unyank, RubyGems yank. Each makes a plan; the stored key is used only after
// the plan is approved. PyPI has no API for yanks: the dialog links to the project page.

/** The actions dialog of one version of a package. */
export function RegistryOperationsDialog({
  packageId,
  name,
  registry,
  version,
  onClose,
}: {
  packageId: number
  name: string
  registry: PackageRegistry
  version: string
  onClose: () => void
}) {
  const t = useRegistries()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const kinds = operationsOf(registry)
  const [kind, setKind] = useState<RegistryPlanKind>(kinds[0] ?? RegistryPlanKind.Yank)
  const [argument, setArgument] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [plan, setPlan] = useState<RegistryPlanDto | null>(null)
  const keyKind = keyKindOf(registry)
  const scope = { TrackedPackageId: packageId }

  const connections = useQuery({
    queryKey: registriesKeys.connections(organization.Id),
    queryFn: () => registriesApi.connections(organization.Id),
    enabled: keyKind != null,
  })
  const hasKey = connections.data?.some((connection) => connection.Kind === keyKind) ?? false

  const create = useMutation({
    mutationFn: () =>
      registriesApi.planOperation(organization.Id, packageId, kind, version, needsArgument(kind) ? argument.trim() : undefined),
    meta: { silentError: true },
    onSuccess: (created) => {
      setPlan(created)
      void queryClient.invalidateQueries({ queryKey: registriesKeys.plans(organization.Id, scopeKey(scope)) })
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (needsArgument(kind) && (!argument.trim() || looksLikeToken(argument))) {
      setError(looksLikeToken(argument) ? t.reminders.tokenRefused : t.operations.argumentRequired)
      return
    }
    setError(null)
    create.mutate()
  }

  if (plan) {
    return <RegistryPlanDialog plan={plan} scope={scope} onClose={onClose} />
  }

  let body
  if (registry === PackageRegistry.PyPI) {
    body = (
      <Callout tone="info">
        {t.operations.pypi}{' '}
        <a
          href={pypiManageUrl(name)}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1 underline underline-offset-2"
        >
          {t.operations.pypiLink}
          <ExternalLink aria-hidden className="size-3.5" />
        </a>
      </Callout>
    )
  } else if (kinds.length === 0) {
    body = <p className="text-fg3">{t.operations.none}</p>
  } else {
    body = (
      <form id="registry-operation-form" className="grid gap-4" onSubmit={submit} noValidate>
        {error ? <FormErrors title={error} /> : null}
        {keyKind != null && connections.isSuccess && !hasKey ? (
          <Callout tone="warning">{interpolate(t.operations.keyMissing, { kind: connectionKindLabel(keyKind, t) })}</Callout>
        ) : null}
        <Field label={t.operations.title}>
          <Select
            aria-label={t.operations.title}
            value={kind}
            options={kinds.map((value) => ({ value, label: planKindLabel(value, t) }))}
            onChange={setKind}
          />
        </Field>
        {kind === RegistryPlanKind.Deprecate ? (
          <Field label={t.operations.message}>
            <Input value={argument} maxLength={500} autoComplete="off" onChange={(event) => setArgument(event.target.value)} />
          </Field>
        ) : null}
        {kind === RegistryPlanKind.DistTag ? (
          <Field label={t.operations.distTag} hint={t.operations.distTagHint}>
            <Input value={argument} maxLength={100} autoComplete="off" onChange={(event) => setArgument(event.target.value)} />
          </Field>
        ) : null}
      </form>
    )
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={interpolate(t.operations.action, { version })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          {kinds.length > 0 ? (
            <Button
              type="submit"
              form="registry-operation-form"
              variant="primary"
              loading={create.isPending}
              disabled={keyKind != null && connections.isSuccess && !hasKey}
            >
              {t.operations.plan}
            </Button>
          ) : null}
        </>
      }
    >
      {body}
    </Modal>
  )
}
