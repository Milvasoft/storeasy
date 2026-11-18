import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Checkbox, Field, Input, RadioGroup } from '@/components/ui/primitives'
import { apiErrorMessage, apiStatus, hasApiKey } from '@/lib/api/http'
import type { AppListDto } from '@/lib/api/types'
import { useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { studioApi, TEMPLATE_NAME_EXISTS_KEY } from './api'
import { useStudioT } from './i18n'
import { studioKeys } from './keys'
import { isLocaleTag, parseLocales } from './spec'
import type { MockupTemplateListDto } from './types'

const BLANK = 'starter:'

/** Design 12.2: a template of one app, from a starter design or as a copy of another template. */
export function NewTemplateModal({
  apps,
  templates,
  initialAppId,
  copyFrom,
  onClose,
  onCreated,
}: {
  apps: AppListDto[]
  templates: MockupTemplateListDto[]
  initialAppId: number | null
  copyFrom: MockupTemplateListDto | null
  onClose: () => void
  onCreated: (templateId: number) => void
}) {
  const s = useStudioT()
  const { lang } = useI18n()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [appId, setAppId] = useState<number | null>(initialAppId ?? copyFrom?.AppId ?? apps[0]?.Id ?? null)
  const [name, setName] = useState(copyFrom ? interpolate(s.create.copyName, { name: copyFrom.Name }) : '')
  const [start, setStart] = useState(copyFrom ? `copy:${copyFrom.Id}` : BLANK)
  const [copyCaptions, setCopyCaptions] = useState(true)
  const [localeText, setLocaleText] = useState((copyFrom?.Locales ?? ['tr']).join(', '))
  const [primary, setPrimary] = useState(copyFrom?.PrimaryLocale ?? 'tr')
  const [submitted, setSubmitted] = useState(false)
  const [nameTaken, setNameTaken] = useState(false)

  const starters = useQuery({
    queryKey: studioKeys.starters(organization.Id),
    queryFn: () => studioApi.starters(organization.Id),
    staleTime: 5 * 60_000,
  })

  const locales = parseLocales(localeText)
  const badLocale = locales.find((locale) => !isLocaleTag(locale))
  const primaryLocale = locales.includes(primary) ? primary : (locales[0] ?? '')
  const copyId = start.startsWith('copy:') ? Number(start.slice(5)) : null
  const starterKey = start.startsWith('starter:') ? start.slice(8) || null : null

  const nameError = submitted && !name.trim() ? s.create.nameRequired : nameTaken ? s.create.nameTaken : null
  const localeError =
    submitted && locales.length === 0
      ? s.create.localesRequired
      : badLocale
        ? interpolate(s.create.localeInvalid, { locale: badLocale })
        : null
  const appError = submitted && !appId ? s.create.appRequired : null

  const create = useMutation({
    mutationFn: () =>
      studioApi.create(organization.Id, {
        AppId: appId ?? 0,
        Name: name.trim(),
        Locales: locales,
        PrimaryLocale: primaryLocale,
        SlotCount: null,
        StarterKey: starterKey,
        CopyFromTemplateId: copyId,
        CopyCaptions: copyId !== null && copyCaptions,
      }),
    meta: { silentError: true },
    onSuccess: (id) => {
      void queryClient.invalidateQueries({ queryKey: studioKeys.all })
      onCreated(id)
    },
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    setNameTaken(false)
    if (!name.trim() || locales.length === 0 || badLocale || !appId) {
      return
    }
    create.mutate(undefined, {
      onError: (error) => {
        if (hasApiKey(error, TEMPLATE_NAME_EXISTS_KEY) || apiStatus(error) === 409) {
          setNameTaken(true)
        }
      },
    })
  }

  const startOptions = [
    { value: BLANK, label: s.create.blank, description: s.create.blankBody },
    ...(starters.data ?? []).map((starter) => ({
      value: `starter:${starter.Key}`,
      label: (lang === 'tr' ? starter.NameTr : null) || starter.Name,
      description: `${(lang === 'tr' ? starter.DescriptionTr : null) || starter.Description || ''} · ${interpolate(s.list.slots, { count: starter.SlotCount })}`,
    })),
    ...templates.map((template) => ({
      value: `copy:${template.Id}`,
      label: interpolate(s.create.copyFrom, { app: template.AppName ?? '', name: template.Name }),
      description: s.create.copyBody,
    })),
  ]
  const failed = create.isError && !nameTaken

  return (
    <Modal
      open
      onClose={onClose}
      title={s.create.title}
      description={s.create.subtitle}
      size="md"
      footer={
        <>
          <Button onClick={onClose}>{s.common.cancel}</Button>
          <Button variant="primary" type="submit" form="studio-new-template" loading={create.isPending}>
            {s.create.submit}
          </Button>
        </>
      }
    >
      <form id="studio-new-template" className="grid gap-4" onSubmit={submit} noValidate>
        <Field label={s.create.app} error={appError}>
          <Select
            value={appId}
            options={apps.map((app) => ({ value: app.Id, label: app.Name }))}
            onChange={setAppId}
            placeholder={s.create.appPlaceholder}
          />
        </Field>
        <Field label={s.create.name} error={nameError}>
          <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
        </Field>
        <RadioGroup name="studio-start" label={s.create.start} value={start} options={startOptions} onChange={setStart} />
        {copyId !== null ? (
          <Checkbox checked={copyCaptions} onChange={setCopyCaptions}>
            {s.create.copyCaptions}
          </Checkbox>
        ) : null}
        <Field label={s.create.locales} hint={s.create.localesHint} error={localeError}>
          <Input value={localeText} onChange={(event) => setLocaleText(event.target.value)} />
        </Field>
        <Field label={s.create.primary}>
          <Select
            value={primaryLocale || null}
            options={locales.map((locale) => ({ value: locale, label: locale }))}
            onChange={setPrimary}
          />
        </Field>
        {failed ? (
          <p className="err-t" role="alert">
            {apiErrorMessage(create.error, s.common.error)}
          </p>
        ) : null}
      </form>
    </Modal>
  )
}
