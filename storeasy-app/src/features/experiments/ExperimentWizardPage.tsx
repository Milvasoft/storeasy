import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import {
  Button,
  CardSkeleton,
  Chip,
  EmptyState,
  Field,
  IconButton,
  Input,
  PageHeader,
  RadioGroup,
  Textarea,
} from '@/components/ui/primitives'
import { listingsApi, mediaApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { contentKeys } from '@/features/common/keys'
import { MediaKind } from '@/features/common/types'
import { studioApi } from '@/features/studio/api'
import { apiErrorMessage } from '@/lib/api/http'
import { jobsApi } from '@/lib/api/endpoints'
import { FilterType } from '@/lib/api/list'
import { StoreJobKind, StoreJobStatus, StoreKind } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { experimentKeys, experimentsApi, useExperiment, useExperimentOverview } from './api'
import { useExperimentsText, type ExperimentsDictionary } from './i18n'
import { kindHint, kindLabel } from './labels'
import { draftProblems, isEditable, KIND_LIMITS, newVariant, supportedKinds, type DraftProblem } from './rules'
import { AsoExperimentKind, type AsoExperimentDto, type AsoExperimentVariantDto, type AsoVariantLocaleDto } from './types'
import { AssetThumb, VariantMedia, type MediaClassOption } from './VariantMedia'

// The create (and draft edit) wizard: the kind the store supports, the name and traffic, the languages, then each
// variant's texts and images (uploads or a Studio mockup export), with the current store page beside the variants. It
// saves a storeasy draft only; nothing goes to the store until a plan is approved on the detail page.

export function problemText(problem: DraftProblem, t: ExperimentsDictionary): string {
  switch (problem.code) {
    case 'name':
      return t.wizard.problem.name
    case 'traffic':
      return t.wizard.problem.traffic
    case 'variantCount':
      return interpolate(t.wizard.problem.variantCount, { max: problem.max })
    case 'variantName':
      return interpolate(t.wizard.problem.variantName, { key: problem.key })
    case 'noLocale':
      return interpolate(t.wizard.problem.noLocale, { key: problem.key })
    case 'tooLong':
      return interpolate(t.wizard.problem.tooLong, {
        key: problem.key,
        locale: problem.locale,
        field: t.wizard[problem.field],
        max: problem.max,
      })
    default:
      return interpolate(t.wizard.problem.empty, { key: problem.key })
  }
}

function withLocales(variant: AsoExperimentVariantDto, locales: string[]): AsoExperimentVariantDto {
  return {
    ...variant,
    Locales: locales.map((locale) => variant.Locales.find((item) => item.Locale === locale) ?? { Locale: locale, Media: [] }),
  }
}

const EXPORT_JOBS_REQUEST = {
  PageNumber: 1,
  RowCount: 20,
  Filtering: {
    Criterias: [
      { FilterBy: 'Kind', Value: StoreJobKind.MockupExport, Type: FilterType.EqualTo },
      { FilterBy: 'Status', Value: StoreJobStatus.Succeeded, Type: FilterType.EqualTo },
    ],
  },
}

/** "Use a mockup export": the finished Studio exports of the app; picking one fills the variant's images. */
function ExportPicker({
  organizationId,
  appId,
  store,
  locales,
  onPick,
}: {
  organizationId: number
  appId: number
  store: StoreKind
  locales: string[]
  onPick: (byLocale: Record<string, AsoVariantLocaleDto['Media']>) => void
}) {
  const t = useExperimentsText()
  const base = useT()
  const format = useFormat()
  const jobs = useQuery({
    queryKey: ['content', 'experiments', 'export-jobs', organizationId, appId],
    queryFn: () => jobsApi.list(organizationId, EXPORT_JOBS_REQUEST, appId),
  })
  const finished = (jobs.data?.Data ?? []).filter(
    (job) => job.Kind === StoreJobKind.MockupExport && job.Status === StoreJobStatus.Succeeded,
  )
  const pick = useMutation({
    mutationFn: (jobId: number) => studioApi.manifest(organizationId, jobId),
    onSuccess: (manifest) => {
      const byLocale: Record<string, AsoVariantLocaleDto['Media']> = {}
      let count = 0
      for (const set of manifest.Sets ?? []) {
        if (set.Store !== store || !set.StoreClassKey || !locales.includes(set.Locale)) {
          continue
        }
        const assetIds = [...(set.Images ?? [])].sort((a, b) => a.SlotOrder - b.SlotOrder).map((image) => image.AssetId)
        count += assetIds.length
        byLocale[set.Locale] = [
          ...(byLocale[set.Locale] ?? []),
          { Kind: MediaKind.Screenshot, ClassKey: set.StoreClassKey, AssetIds: assetIds },
        ]
      }
      onPick(byLocale)
      showToast(interpolate(t.wizard.exportApplied, { count }), 'success')
    },
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })
  if (jobs.isLoading) {
    return null
  }
  return (
    <Field label={t.wizard.fromExport} hint={finished.length > 0 ? t.wizard.fromExportHint : t.wizard.noExports}>
      <Select<number>
        value={null}
        disabled={finished.length === 0 || pick.isPending}
        placeholder={t.wizard.fromExport}
        options={finished.map((job) => ({
          value: job.Id,
          label: interpolate(t.wizard.exportJob, { id: job.Id, date: format.date(job.CreationDate ?? null) }),
        }))}
        onChange={(jobId) => pick.mutate(jobId)}
      />
    </Field>
  )
}

function Wizard({ context, existing }: { context: WorkspaceContext; existing: AsoExperimentDto | null }) {
  const t = useExperimentsText()
  const base = useT()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { app, record, organizationId, storeQuery } = context
  const overview = useExperimentOverview(organizationId, app.Id)
  const capability = overview.data?.Capabilities.find((item) => item.StoreAppId === record.StoreAppId)
  const kinds = existing ? [existing.Kind] : supportedKinds(capability)

  const [kind, setKind] = useState<AsoExperimentKind | null>(existing?.Kind ?? null)
  const [name, setName] = useState(existing?.Name ?? '')
  const [traffic, setTraffic] = useState<string>(existing?.TrafficProportion != null ? String(existing.TrafficProportion) : '50')
  const [note, setNote] = useState(existing?.Note ?? '')
  const [locales, setLocales] = useState<string[]>(
    existing ? [...new Set(existing.Variants.flatMap((v) => v.Locales.map((l) => l.Locale)))] : [],
  )
  const [variants, setVariants] = useState<AsoExperimentVariantDto[]>(existing?.Variants ?? [])
  const [tried, setTried] = useState(false)

  const activeKind = kind ?? kinds[0] ?? null
  const limits = activeKind ? KIND_LIMITS[activeKind] : null

  const listing = useQuery({
    queryKey: contentKeys.listing(organizationId, record.StoreAppId, null),
    queryFn: () => listingsApi.get(organizationId, record.StoreAppId, null),
    enabled: record.Store !== StoreKind.Microsoft,
  })
  const listingLocales = listing.data?.Locales ?? []
  const primary = listingLocales.find((item) => item.IsPrimary)?.Locale ?? listingLocales[0]?.Locale ?? null
  const previewLocale = locales[0] ?? primary

  // Start with the primary language and one variant once the listing is known.
  useEffect(() => {
    if (!existing && locales.length === 0 && primary) {
      setLocales([primary])
      setVariants((current) => (current.length === 0 ? [newVariant([], [primary])] : current.map((v) => withLocales(v, [primary]))))
    }
  }, [existing, locales.length, primary])

  const screenshotSets = useQuery({
    queryKey: contentKeys.media(organizationId, record.StoreAppId, MediaKind.Screenshot, previewLocale),
    queryFn: () => mediaApi.screenshotSets(organizationId, record.StoreAppId, previewLocale),
    enabled: !!previewLocale,
  })
  const previewSets = useQuery({
    queryKey: contentKeys.media(organizationId, record.StoreAppId, MediaKind.Preview, previewLocale),
    queryFn: () => mediaApi.previewSets(organizationId, record.StoreAppId, previewLocale),
    enabled: !!previewLocale && record.Store === StoreKind.Apple,
  })
  const classes: MediaClassOption[] = useMemo(
    () => [
      ...(screenshotSets.data?.Classes ?? []).map((item) => ({
        kind: MediaKind.Screenshot,
        classKey: item.ClassKey,
        label: item.Title ?? item.ClassKey,
      })),
      ...(activeKind === AsoExperimentKind.CustomProductPage || activeKind === AsoExperimentKind.ProductPageOptimization
        ? (previewSets.data?.Classes ?? []).map((item) => ({
            kind: MediaKind.Preview,
            classKey: item.ClassKey,
            label: item.Title ?? item.ClassKey,
          }))
        : []),
    ],
    [screenshotSets.data, previewSets.data, activeKind],
  )

  const save = useMutation({
    mutationFn: () =>
      experimentsApi.save(organizationId, {
        Id: existing?.Id ?? null,
        AppId: app.Id,
        StoreAppId: record.StoreAppId,
        Kind: activeKind as AsoExperimentKind,
        Name: name.trim(),
        TrafficProportion: limits?.traffic ? Number(traffic) : null,
        Variants: variants.map((variant) => ({ ...variant, Name: variant.Name.trim(), AppIconName: variant.AppIconName?.trim() || null })),
        Note: note.trim() || null,
      }),
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: experimentKeys.all })
      showToast(t.wizard.saved, 'success')
      navigate(`/apps/${app.Id}/experiments/${saved.Id}${storeQuery}`)
    },
    onError: (error) => showToast(apiErrorMessage(error, base.common.error), 'error'),
  })

  if (overview.isLoading || listing.isLoading) {
    return <CardSkeleton />
  }
  if (overview.isError) {
    return <ScreenState error={overview.error} onRetry={() => void overview.refetch()} />
  }
  if (!activeKind || !limits) {
    return <EmptyState title={t.wizard.notSupported} />
  }

  const problems = draftProblems({ kind: activeKind, name, traffic: limits.traffic ? Number(traffic) : null, variants })
  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTried(true)
    if (problems.length === 0) {
      save.mutate()
    }
  }
  const setVariant = (key: string, next: AsoExperimentVariantDto) =>
    setVariants((current) => current.map((v) => (v.Key === key ? next : v)))
  const setLocale = (variant: AsoExperimentVariantDto, locale: string, next: Partial<AsoVariantLocaleDto>) =>
    setVariant(variant.Key, { ...variant, Locales: variant.Locales.map((item) => (item.Locale === locale ? { ...item, ...next } : item)) })
  const toggleLocale = (locale: string) => {
    const next = locales.includes(locale) ? locales.filter((item) => item !== locale) : [...locales, locale]
    setLocales(next)
    setVariants((current) => current.map((variant) => withLocales(variant, next)))
  }
  const changeKind = (next: AsoExperimentKind) => {
    setKind(next)
    setVariants((current) => current.slice(0, KIND_LIMITS[next].maxVariants))
  }

  const currentText = (field: string) =>
    listing.data?.Fields?.find((item) => item.Key === field && item.Locale === previewLocale)?.Value ?? ''
  const currentImages = (screenshotSets.data?.Classes ?? []).find((item) => (item.Items ?? []).length > 0)?.Items ?? []
  const textFields = (['promotionalText', 'shortDescription', 'fullDescription'] as const).filter((field) => limits[field] != null)

  return (
    <form className="grid gap-5" noValidate onSubmit={submit}>
      <PageHeader title={existing ? t.wizard.editTitle : t.wizard.title} />

      {!existing ? (
        <RadioGroup<AsoExperimentKind>
          name="kind"
          label={t.wizard.kind}
          value={activeKind}
          options={kinds.map((item) => ({ value: item, label: kindLabel(item, t), description: kindHint(item, t) }))}
          onChange={changeKind}
        />
      ) : null}

      <div className="grid max-w-xl gap-4">
        <Field label={t.wizard.name}>
          <Input value={name} placeholder={t.wizard.namePlaceholder} maxLength={200} onChange={(event) => setName(event.target.value)} />
        </Field>
        {limits.traffic ? (
          <Field label={t.wizard.traffic} hint={t.wizard.trafficHint}>
            <Input type="number" min={1} max={100} step={1} value={traffic} onChange={(event) => setTraffic(event.target.value)} />
          </Field>
        ) : null}
        <Field label={t.wizard.note} optional>
          <Textarea rows={2} value={note} maxLength={1000} onChange={(event) => setNote(event.target.value)} />
        </Field>
      </div>

      <section aria-label={t.wizard.locales} className="grid gap-2">
        <h2 className="m-0 text-[15px] font-semibold">{t.wizard.locales}</h2>
        <p className="m-0 text-xs text-fg3">{t.wizard.localesHint}</p>
        <div className="toolbar" role="group" aria-label={t.wizard.locales}>
          {listingLocales.map((item) => (
            <Chip key={item.Locale} selected={locales.includes(item.Locale)} onClick={() => toggleLocale(item.Locale)}>
              <span className="mono">{item.Locale}</span>
            </Chip>
          ))}
        </div>
      </section>

      <section aria-label={t.wizard.variants} className="grid gap-3">
        <div className="flex items-center gap-2">
          <h2 className="m-0 flex-1 text-[15px] font-semibold">{t.wizard.variants}</h2>
          <Button
            variant="outline"
            size="sm"
            disabled={variants.length >= limits.maxVariants}
            onClick={() => setVariants((current) => [...current, newVariant(current, locales)])}
          >
            <Plus className="h-4 w-4" aria-hidden />
            {t.wizard.addVariant}
          </Button>
        </div>
        {variants.map((variant) => (
          <article key={variant.Key} className="card grid gap-3 p-4" aria-label={interpolate(t.variantLabel, { key: variant.Key })}>
            <div className="flex items-center gap-2">
              <b className="flex-1">{interpolate(t.variantLabel, { key: variant.Key })}</b>
              {variants.length > limits.minVariants ? (
                <IconButton
                  label={interpolate(t.wizard.removeVariant, { key: variant.Key })}
                  onClick={() => setVariants((current) => current.filter((v) => v.Key !== variant.Key))}
                >
                  <Trash2 className="h-4 w-4" aria-hidden />
                </IconButton>
              ) : null}
            </div>
            <div className="grid max-w-xl gap-3">
              <Field label={t.wizard.variantName}>
                <Input
                  value={variant.Name}
                  maxLength={100}
                  onChange={(event) => setVariant(variant.Key, { ...variant, Name: event.target.value })}
                />
              </Field>
              {limits.appIcon ? (
                <Field label={t.wizard.appIconName} hint={t.wizard.appIconHint} optional>
                  <Input
                    mono
                    value={variant.AppIconName ?? ''}
                    onChange={(event) => setVariant(variant.Key, { ...variant, AppIconName: event.target.value })}
                  />
                </Field>
              ) : null}
            </div>
            <ExportPicker
              organizationId={organizationId}
              appId={app.Id}
              store={record.Store}
              locales={locales}
              onPick={(byLocale) =>
                setVariant(variant.Key, {
                  ...variant,
                  Locales: variant.Locales.map((item) => (byLocale[item.Locale] ? { ...item, Media: byLocale[item.Locale] } : item)),
                })
              }
            />
            {variant.Locales.map((locale) => (
              <fieldset key={locale.Locale} className="grid gap-3 rounded border border-line p-3">
                <legend className="mono px-1 text-xs">{locale.Locale}</legend>
                {textFields.map((field) => {
                  const max = limits[field] ?? 0
                  const key =
                    field === 'promotionalText' ? 'PromotionalText' : field === 'shortDescription' ? 'ShortDescription' : 'FullDescription'
                  const value = locale[key] ?? ''
                  return (
                    <Field key={field} label={t.wizard[field]} optional counter={{ value: [...value].length, max }}>
                      <Textarea
                        rows={field === 'fullDescription' ? 6 : 2}
                        value={value}
                        onChange={(event) => setLocale(variant, locale.Locale, { [key]: event.target.value })}
                      />
                    </Field>
                  )
                })}
                <div>
                  <div className="mb-1 text-[13px] font-semibold">{t.wizard.media}</div>
                  <VariantMedia
                    organizationId={organizationId}
                    value={locale.Media}
                    classes={classes}
                    onChange={(media) => setLocale(variant, locale.Locale, { Media: media })}
                  />
                </div>
              </fieldset>
            ))}
          </article>
        ))}
      </section>

      <section aria-label={t.wizard.preview} className="grid gap-2">
        <h2 className="m-0 text-[15px] font-semibold">{t.wizard.preview}</h2>
        <div className="grid gap-3" style={{ gridTemplateColumns: `repeat(${variants.length + 1}, minmax(0, 1fr))` }}>
          <div className="card grid content-start gap-2 p-3" aria-label={t.wizard.current}>
            <b>{t.wizard.current}</b>
            {textFields.map((field) => (
              <p key={field} className="m-0 text-xs text-fg2">
                {currentText(field) || '-'}
              </p>
            ))}
            {currentImages.length > 0 ? (
              <div className="flex flex-wrap gap-1">
                {currentImages
                  .slice(0, 3)
                  .map((item) =>
                    item.Url ? (
                      <img key={item.Id} src={item.Url} alt={item.FileName ?? item.Id} className="h-20 w-12 rounded object-cover" />
                    ) : null,
                  )}
              </div>
            ) : (
              <p className="m-0 text-xs text-fg3">{t.wizard.noCurrentImages}</p>
            )}
          </div>
          {variants.map((variant) => {
            const locale = variant.Locales.find((item) => item.Locale === previewLocale) ?? variant.Locales[0]
            const assets = (locale?.Media ?? []).flatMap((media) => media.AssetIds)
            return (
              <div key={variant.Key} className="card grid content-start gap-2 p-3">
                <b>{variant.Name.trim() || interpolate(t.variantLabel, { key: variant.Key })}</b>
                {variant.AppIconName ? <span className="mono text-xs">{variant.AppIconName}</span> : null}
                {textFields.map((field) => {
                  const value =
                    field === 'promotionalText'
                      ? locale?.PromotionalText
                      : field === 'shortDescription'
                        ? locale?.ShortDescription
                        : locale?.FullDescription
                  return (
                    <p key={field} className="m-0 text-xs text-fg2">
                      {value || currentText(field) || '-'}
                    </p>
                  )
                })}
                <div className="flex flex-wrap gap-1">
                  {assets.slice(0, 3).map((assetId) => (
                    <AssetThumb key={assetId} organizationId={organizationId} assetId={assetId} label={`${variant.Key} #${assetId}`} />
                  ))}
                </div>
                <span className="text-xs text-fg3">{interpolate(t.wizard.images, { count: assets.length })}</span>
              </div>
            )
          })}
        </div>
      </section>

      {tried && problems.length > 0 ? (
        <Callout tone="danger">
          <ul className="m-0 pl-4">
            {problems.map((problem, index) => (
              <li key={index}>{problemText(problem, t)}</li>
            ))}
          </ul>
        </Callout>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" variant="primary" loading={save.isPending}>
          {t.wizard.save}
        </Button>
        <Link
          className="btn"
          to={existing ? `/apps/${app.Id}/experiments/${existing.Id}${storeQuery}` : `/apps/${app.Id}/experiments${storeQuery}`}
        >
          {t.wizard.cancel}
        </Link>
      </div>
    </form>
  )
}

function EditWizard({ context, id }: { context: WorkspaceContext; id: number }) {
  const t = useExperimentsText()
  const experiment = useExperiment(context.organizationId, id)
  if (experiment.isLoading) {
    return <CardSkeleton />
  }
  if (experiment.isError || !experiment.data) {
    return <ScreenState error={experiment.error} onRetry={() => void experiment.refetch()} />
  }
  if (!isEditable(experiment.data.Status)) {
    return <EmptyState title={t.wizard.editTitle} description={t.detail.noActions} />
  }
  return <Wizard context={context} existing={experiment.data} />
}

export function ExperimentWizardPage() {
  const { experimentId } = useParams()
  const id = Number(experimentId ?? 0)
  return (
    <AppWorkspace section="experiments">
      {(context) =>
        !context.editor ? null : id > 0 ? <EditWizard context={context} id={id} /> : <Wizard context={context} existing={null} />
      }
    </AppWorkspace>
  )
}
