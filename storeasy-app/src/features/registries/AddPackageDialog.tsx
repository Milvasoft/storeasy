import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { appsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { useT } from '@/lib/i18n/I18nContext'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { registriesApi } from './api'
import { useRegistries } from './i18n/useRegistries'
import { registriesKeys } from './keys'
import { REGISTRIES, isPackageName, registryLabel } from './rules'
import { PackageRegistry } from './types'

// The apps a package can be linked to (the first 100 by the list's own order).
const APPS_REQUEST = { PageNumber: 1, RowCount: 100 }

const HINT_KEY = {
  [PackageRegistry.Npm]: 'npm',
  [PackageRegistry.NuGet]: 'nuget',
  [PackageRegistry.PyPI]: 'pypi',
  [PackageRegistry.Crates]: 'crates',
} as const

/** Starts tracking a package by registry and name; the API checks the name on the registry and queues the first sync. */
export function AddPackageDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const base = useT()
  const t = useRegistries()
  const organization = useCurrentOrganization()
  const { watchJobs } = useActivity()
  const queryClient = useQueryClient()
  const [registry, setRegistry] = useState<PackageRegistry>(PackageRegistry.Npm)
  const [name, setName] = useState('')
  // 0: no app.
  const [appId, setAppId] = useState<number>(0)
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  const apps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APPS_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APPS_REQUEST),
    enabled: open,
  })

  const close = () => {
    setName('')
    setAppId(0)
    setError(null)
    setServer(null)
    onClose()
  }

  const add = useMutation({
    mutationFn: () => registriesApi.add(organization.Id, registry, name.trim(), appId || null),
    meta: { silentError: true },
    onSuccess: (added) => {
      watchJobs([added.JobId])
      void queryClient.invalidateQueries({ queryKey: registriesKeys.all })
      showToast(t.add.added, 'success', added.Name)
      close()
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = isPackageName(registry, name) ? null : t.add.nameInvalid
    setError(problem)
    setServer(null)
    if (!problem) {
      add.mutate()
    }
  }

  const appOptions = [{ value: 0, label: t.add.noApp }, ...(apps.data?.Data ?? []).map((app) => ({ value: app.Id, label: app.Name }))]

  return (
    <Modal
      open={open}
      onClose={close}
      title={t.add.title}
      description={t.add.body}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="add-registry-package-form" variant="primary" loading={add.isPending}>
            {t.add.submit}
          </Button>
        </>
      }
    >
      <form id="add-registry-package-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        <Field label={t.add.registry}>
          <Select
            aria-label={t.add.registry}
            value={registry}
            options={REGISTRIES.map((value) => ({ value, label: registryLabel(value) }))}
            onChange={setRegistry}
          />
        </Field>
        <Field label={t.add.name} hint={t.add.nameHint[HINT_KEY[registry]]} error={error}>
          <Input
            mono
            value={name}
            autoComplete="off"
            spellCheck={false}
            maxLength={214}
            onChange={(event) => setName(event.target.value)}
          />
        </Field>
        <Field label={t.add.app} hint={t.add.appHint} optional>
          <Select aria-label={t.add.app} value={appId} options={appOptions} onChange={setAppId} searchable />
        </Field>
      </form>
    </Modal>
  )
}
