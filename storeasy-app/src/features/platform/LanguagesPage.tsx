import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Languages } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Chip, EmptyState, PageHeader, Switch, TableSkeleton } from '@/components/ui/primitives'
import { FilterType, type ListRequest } from '@/lib/api/list'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { showToast } from '@/lib/toast/toast'
import { platformApi, platformKeys } from './api'
import { MAX_ROWS, PlatformGate, PlatformPermission } from './PlatformCommon'
import { usePlatformText } from './i18n'
import { updated, type LanguageDto, type UpdateLanguageCommand } from './types'

// Design 17.21: the server's message languages. A fixed seed (no add or delete); supported switch and default radio inline.
// Rules drawn here (the API does not check them yet): the default language cannot be turned off, an unsupported one cannot
// become the default.

function languagesRequest(supportedOnly: boolean): ListRequest {
  return {
    PageNumber: 1,
    RowCount: MAX_ROWS,
    ...(supportedOnly ? { Filtering: { Criterias: [{ FilterBy: 'Supported', Value: true, Type: FilterType.EqualTo }] } } : {}),
  }
}

function LanguagesList() {
  const t = usePlatformText()
  const base = useT()
  const confirm = useConfirm()
  const queryClient = useQueryClient()
  const global = useGlobalPermissions()
  const [supportedOnly, setSupportedOnly] = useState(false)
  const request = languagesRequest(supportedOnly)
  const languages = useQuery({ queryKey: [...platformKeys.languages(), request], queryFn: () => platformApi.languages(request) })
  const canUpdate = global.has(PlatformPermission.LanguageUpdate)
  const save = useMutation({
    mutationFn: (command: UpdateLanguageCommand) => platformApi.updateLanguage(command),
    onSuccess: () => {
      showToast(t.languages.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: platformKeys.languages() })
    },
  })
  const busyId = save.isPending ? save.variables?.Id : undefined

  const makeDefault = async (language: LanguageDto) => {
    const ok = await confirm({
      title: t.languages.defaultConfirm.title,
      message: interpolate(t.languages.defaultConfirm.body, { name: language.Name }),
      confirmLabel: t.languages.defaultConfirm.confirm,
    })
    if (ok) {
      save.mutate({ Id: language.Id, IsDefault: updated(true) })
    }
  }

  const columns: Column<LanguageDto>[] = [
    { key: 'name', header: t.languages.language, role: 'main', cell: (language) => <b>{language.Name}</b> },
    { key: 'code', header: t.languages.code, cell: (language) => <span className="mono text-xs">{language.Code}</span> },
    {
      key: 'supported',
      header: t.languages.supported,
      cell: (language) => (
        <span className="inline-flex items-center gap-2">
          <Switch
            checked={language.Supported}
            disabled={!canUpdate || language.IsDefault || busyId === language.Id}
            label={`${t.languages.supported}: ${language.Name}`}
            onChange={(value) => save.mutate({ Id: language.Id, Supported: updated(value) })}
          />
          {language.IsDefault ? <span className="text-xs text-fg3">{t.languages.defaultLocked}</span> : null}
        </span>
      ),
    },
    {
      key: 'default',
      header: t.languages.isDefault,
      cell: (language) =>
        language.IsDefault ? (
          <Badge tone="success" small dot>
            {t.languages.isDefault}
          </Badge>
        ) : (
          <span className="inline-flex items-center gap-2">
            <label className={cn('inline-flex items-center', (!canUpdate || !language.Supported) && 'cursor-not-allowed opacity-60')}>
              <input
                type="radio"
                className="peer sr-only"
                name="default-language"
                checked={false}
                disabled={!canUpdate || !language.Supported || busyId === language.Id}
                aria-label={interpolate(t.languages.makeDefault, { name: language.Name })}
                onChange={() => void makeDefault(language)}
              />
              <span className="rad" aria-hidden />
            </label>
            {!language.Supported ? <span className="text-xs text-fg3">{t.languages.enableFirst}</span> : null}
          </span>
        ),
    },
  ]
  const rows = languages.data?.Data ?? []

  return (
    <>
      <PageHeader title={t.languages.title} subtitle={t.languages.subtitle} />
      <div className="toolbar">
        <Chip selected={supportedOnly} onClick={() => setSupportedOnly((value) => !value)}>
          {t.languages.supportedOnly}
        </Chip>
      </div>
      {!canUpdate ? (
        <div className="mb-3">
          <Callout tone="info">{t.languages.readOnly}</Callout>
        </div>
      ) : null}
      {languages.isLoading ? (
        <TableSkeleton columns={[t.languages.language, t.languages.code, t.languages.supported, t.languages.isDefault]} label={base.common.loading} />
      ) : languages.isError ? (
        <ScreenState error={languages.error} onRetry={() => void languages.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<Languages aria-hidden />} title={t.languages.empty} />
      ) : (
        <DataTable caption={t.languages.title} columns={columns} rows={rows} rowKey={(language) => language.Id} busy={languages.isFetching} />
      )}
      <p className="mt-3 text-xs text-fg3">{t.languages.note}</p>
    </>
  )
}

/** /admin/languages. */
export function LanguagesPage() {
  return (
    <PlatformGate permission={PlatformPermission.LanguageList}>
      <LanguagesList />
    </PlatformGate>
  )
}
