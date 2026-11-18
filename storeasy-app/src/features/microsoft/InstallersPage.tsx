import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, ShieldCheck, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, Field, Input, PageHeader } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { splitList } from '@/features/common/text'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { installerTypeLabel, signatureView } from './labels'
import { MsWorkspace, useMsPlan, type MsContext } from './MsWorkspace'
import { ARCHITECTURES, installerProblems } from './rules'
import { MsChangeKind, MsInstallerType, MsSignatureStatus, type MsInstallerPackageDto, type MsInstallerPackageInputDto } from './types'

// Design 8.5: MSI and EXE installers stay on the developer's server; the Store gets the address, architecture, languages
// and silent install switches. storeasy checks the Authenticode signature of each (a job downloads it); the Store refuses
// an unsigned installer. The whole set is planned at once.

interface Row extends Omit<MsInstallerPackageInputDto, 'Languages'> {
  key: number
  languages: string
}

function rowOf(input: MsInstallerPackageInputDto, key: number): Row {
  return { ...input, key, languages: input.Languages.join(', ') }
}

function inputOf(row: Row): MsInstallerPackageInputDto {
  return {
    PackageUrl: row.PackageUrl.trim(),
    PackageType: row.PackageType,
    Architecture: row.Architecture ?? null,
    Languages: splitList(row.languages).map((language) => language.toLowerCase()),
    IsSilentInstall: row.IsSilentInstall,
    InstallerParameters: row.InstallerParameters?.trim() || null,
    GenericDocUrl: row.GenericDocUrl?.trim() || null,
  }
}

function sameSet(rows: Row[], original: MsInstallerPackageInputDto[]): boolean {
  return JSON.stringify(rows.map(inputOf)) === JSON.stringify(original.map((item) => inputOf(rowOf(item, 0))))
}

function Signature({ checked }: { checked: MsInstallerPackageDto | undefined }) {
  const t = useMs()
  const format = useFormat()
  const view = signatureView(checked?.SignatureStatus ?? MsSignatureStatus.Unchecked, t)
  return (
    <span className="grid gap-0.5 text-xs">
      <span>
        <Badge small tone={view.tone}>
          {view.label}
        </Badge>
      </span>
      {checked?.SignatureNote ? <span className="text-fg2">{checked.SignatureNote}</span> : null}
      {checked?.CheckedDate ? (
        <span className="text-fg3">{interpolate(t.installers.checked, { date: format.dateTime(checked.CheckedDate) })}</span>
      ) : null}
    </span>
  )
}

function Installers({
  context,
  original,
  checks,
}: {
  context: MsContext
  original: MsInstallerPackageInputDto[]
  checks: MsInstallerPackageDto[]
}) {
  const t = useMs()
  const { organizationId, record, editable, editor } = context
  const queryClient = useQueryClient()
  const { watchJobs } = useActivity()
  const [rows, setRows] = useState<Row[]>(() => original.map(rowOf))
  const [nextKey, setNextKey] = useState(original.length)
  const plan = useMsPlan(context)

  const verify = useMutation({
    mutationFn: (urls: string[]) => msApi.verifyPackages(organizationId, record.StoreAppId, urls),
    onSuccess: (jobId) => {
      watchJobs([jobId])
      showToast(t.installers.verifyQueued, 'success')
      void queryClient.invalidateQueries({ queryKey: msKeys.packages(organizationId, record.StoreAppId) })
    },
  })

  const problems = rows.map((row) => installerProblems(inputOf(row)))
  const invalid = problems.some((list) => list.length > 0)
  const changed = !sameSet(rows, original)
  const unsigned = checks.some((check) => check.SignatureStatus === MsSignatureStatus.Unsigned)
  const update = (key: number, patch: Partial<Row>) => setRows((list) => list.map((row) => (row.key === key ? { ...row, ...patch } : row)))

  const add = () => {
    setRows((list) => [
      ...list,
      {
        key: nextKey,
        PackageUrl: '',
        PackageType: MsInstallerType.Exe,
        Architecture: 'X64',
        languages: '',
        IsSilentInstall: false,
        InstallerParameters: '',
        GenericDocUrl: '',
      },
    ])
    setNextKey((key) => key + 1)
  }

  return (
    <>
      <PageHeader
        title={t.installers.title}
        subtitle={t.installers.lead}
        actions={
          editor && checks.length > 0 ? (
            <Button loading={verify.isPending} onClick={() => verify.mutate([])}>
              <ShieldCheck aria-hidden />
              {t.installers.verifyAll}
            </Button>
          ) : undefined
        }
      />
      {plan.element}
      {unsigned ? (
        <div className="mb-3">
          <Callout tone="danger">{t.installers.unsignedNote}</Callout>
        </div>
      ) : null}
      {rows.length === 0 ? <p className="text-[13px] text-fg3">{t.installers.none}</p> : null}
      <div className="grid gap-3">
        {rows.map((row, index) => {
          const check = checks.find((item) => item.PackageUrl === row.PackageUrl.trim())
          const rowProblems = problems[index]
          const label = `${installerTypeLabel(row.PackageType, t)} · ${row.Architecture ?? '-'}`
          return (
            <section key={row.key} className="card" aria-label={row.PackageUrl || label}>
              <div className="card-h flex-wrap">
                <b className="min-w-0 flex-1 text-[13px]">{label}</b>
                <Signature checked={check} />
                {editor && check ? (
                  <Button size="sm" loading={verify.isPending} onClick={() => verify.mutate([check.PackageUrl])}>
                    {t.installers.verify}
                  </Button>
                ) : null}
              </div>
              <div className="card-b grid gap-3 md:grid-cols-2">
                <div className="md:col-span-2">
                  <Field label={t.installers.url} error={rowProblems.includes('url') && row.PackageUrl ? t.installers.problem.url : null}>
                    <Input
                      mono
                      type="url"
                      value={row.PackageUrl}
                      disabled={!editable}
                      onChange={(event) => update(row.key, { PackageUrl: event.target.value })}
                    />
                  </Field>
                </div>
                <Field label={t.installers.type}>
                  <Select
                    aria-label={t.installers.type}
                    value={row.PackageType}
                    disabled={!editable}
                    options={[
                      { value: MsInstallerType.Exe, label: t.enums.installerType.exe },
                      { value: MsInstallerType.Msi, label: t.enums.installerType.msi },
                    ]}
                    onChange={(value) => update(row.key, { PackageType: value })}
                  />
                </Field>
                <Field label={t.installers.architecture}>
                  <Select
                    aria-label={t.installers.architecture}
                    value={row.Architecture ?? null}
                    disabled={!editable}
                    options={ARCHITECTURES.map((value) => ({ value, label: value }))}
                    onChange={(value) => update(row.key, { Architecture: value })}
                  />
                </Field>
                <Field
                  label={t.installers.languages}
                  hint={t.installers.languagesHint}
                  error={rowProblems.includes('languages') && row.PackageUrl ? t.installers.problem.languages : null}
                >
                  <Input
                    mono
                    value={row.languages}
                    disabled={!editable}
                    onChange={(event) => update(row.key, { languages: event.target.value })}
                  />
                </Field>
                <Field
                  label={t.installers.parameters}
                  error={rowProblems.includes('parameters') && row.PackageUrl ? t.installers.problem.parameters : null}
                >
                  <Input
                    mono
                    value={row.InstallerParameters ?? ''}
                    disabled={!editable || row.IsSilentInstall}
                    onChange={(event) => update(row.key, { InstallerParameters: event.target.value })}
                  />
                </Field>
                <Checkbox
                  checked={row.IsSilentInstall}
                  disabled={!editable}
                  onChange={(checked) => update(row.key, { IsSilentInstall: checked })}
                >
                  {t.installers.silent}
                </Checkbox>
                <Field
                  label={t.installers.docUrl}
                  error={rowProblems.includes('docUrl') && row.PackageUrl ? t.installers.problem.docUrl : null}
                >
                  <Input
                    mono
                    type="url"
                    value={row.GenericDocUrl ?? ''}
                    disabled={!editable}
                    onChange={(event) => update(row.key, { GenericDocUrl: event.target.value })}
                  />
                </Field>
              </div>
              {editable ? (
                <div className="card-f justify-end">
                  <Button
                    size="sm"
                    variant="danger-outline"
                    aria-label={`${t.installers.remove}: ${row.PackageUrl || label}`}
                    onClick={() => setRows((list) => list.filter((item) => item.key !== row.key))}
                  >
                    <Trash2 aria-hidden />
                    {t.installers.remove}
                  </Button>
                </div>
              ) : null}
            </section>
          )
        })}
      </div>
      {editable ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button onClick={add}>
            <Plus aria-hidden />
            {t.installers.add}
          </Button>
          <Button
            variant="primary"
            disabled={!changed || invalid}
            loading={plan.pending}
            onClick={() => plan.make({ Kind: MsChangeKind.InstallerPackages, InstallerPackages: rows.map(inputOf) })}
          >
            {t.planMake}
          </Button>
          {changed && invalid ? <span className="err-t">{t.installers.fix}</span> : null}
        </div>
      ) : null}
    </>
  )
}

function InstallersScreen({ context }: { context: MsContext }) {
  const { organizationId, record } = context
  const content = useQuery({
    queryKey: msKeys.content(organizationId, record.StoreAppId, null, null),
    queryFn: () => msApi.content(organizationId, record.StoreAppId),
  })
  const checks = useQuery({
    queryKey: msKeys.packages(organizationId, record.StoreAppId),
    queryFn: () => msApi.packages(organizationId, record.StoreAppId),
  })
  if (content.isLoading || checks.isLoading) {
    return <CardSkeleton />
  }
  if (content.isError || !content.data) {
    return <ScreenState error={content.error} onRetry={() => void content.refetch()} />
  }
  return <Installers context={context} original={content.data.InstallerPackages ?? []} checks={checks.data ?? []} />
}

export function MsInstallersPage() {
  return <MsWorkspace sub="installers">{(context) => <InstallersScreen context={context} />}</MsWorkspace>
}
