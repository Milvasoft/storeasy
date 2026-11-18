import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/Badge'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Field, Input, PageHeader } from '@/components/ui/primitives'
import { AssetPurpose } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { charCount } from '@/features/common/text'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { MsWorkspace, useMsPlan, type MsContext } from './MsWorkspace'
import { DESKTOP_SCREENSHOT, LOGO_TYPES, MS_LIMITS, SCREENSHOT_TYPES, imageLimit } from './rules'
import { MsChangeKind, type MsContentDto, type MsImageDto } from './types'

// Design 7.17: screenshots and logos of one language. Up to 10 desktop screenshots and 8 of every other family (the
// catalog's microsoft.screenshots rules); images travel with the submission, so a change is a plan like the texts.

const IMAGE_TYPES: string[] = [...SCREENSHOT_TYPES, ...LOGO_TYPES]

interface NewImage {
  AssetId: number
  ImageType: string
  Description: string | null
  FileName: string
}

function Images({ context, content }: { context: MsContext; content: MsContentDto }) {
  const t = useMs()
  const { editable } = context
  const images = content.Images ?? []
  const locales = [
    ...new Set([...(content.Listings ?? []).map((listing) => listing.Locale), ...images.map((image) => image.Locale ?? '')]),
  ].filter(Boolean)
  const [locale, setLocale] = useState(locales[0] ?? '')
  const [removed, setRemoved] = useState<string[]>([])
  const [added, setAdded] = useState<NewImage[]>([])
  const [type, setType] = useState<string>(DESKTOP_SCREENSHOT)
  const [caption, setCaption] = useState('')
  const plan = useMsPlan(context, () => {
    setRemoved([])
    setAdded([])
  })

  const ofLocale = images.filter((image) => image.Locale === locale)
  const countOf = (imageType: string) =>
    ofLocale.filter((image) => image.ImageType === imageType && !removed.includes(image.FileName ?? '')).length +
    added.filter((image) => image.ImageType === imageType).length
  const full = countOf(type) >= imageLimit(type)
  const captionOver = charCount(caption) > MS_LIMITS.imageCaption
  const groups = IMAGE_TYPES.filter(
    (imageType) => ofLocale.some((image) => image.ImageType === imageType) || added.some((image) => image.ImageType === imageType),
  )

  const pick = (next: string) => {
    setLocale(next)
    setRemoved([])
    setAdded([])
  }

  const toggle = (image: MsImageDto) => {
    const name = image.FileName ?? ''
    setRemoved((list) => (list.includes(name) ? list.filter((item) => item !== name) : [...list, name]))
  }

  return (
    <>
      <PageHeader title={t.images.title} subtitle={t.images.lead} />
      {plan.element}
      <div className="toolbar mb-3" role="group" aria-label={t.images.language}>
        {locales.map((item) => (
          <button
            key={item}
            type="button"
            className={cn('chip mono', item === locale && 'on')}
            aria-pressed={item === locale}
            onClick={() => pick(item)}
          >
            {item}
          </button>
        ))}
      </div>
      <div className="grid gap-4">
        {groups.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.images.empty}</p> : null}
        {groups.map((imageType) => (
          <section key={imageType} className="card" aria-label={imageType}>
            <div className="card-h">
              <h2 className="h3 flex-1 mono">{imageType}</h2>
              <span className="text-xs text-fg3">
                {interpolate(t.images.count, { count: countOf(imageType), max: imageLimit(imageType) })}
              </span>
            </div>
            <ul className="card-b m-0 grid list-none gap-2 p-0">
              {ofLocale
                .filter((image) => image.ImageType === imageType)
                .map((image) => {
                  const gone = removed.includes(image.FileName ?? '')
                  return (
                    <li key={image.FileName ?? image.Id} className="flex flex-wrap items-center gap-2 text-[13px]">
                      <span className={cn('mono min-w-0 flex-1 truncate', gone && 'line-through text-fg3')}>{image.FileName}</span>
                      {image.Description ? <span className="text-fg2">{image.Description}</span> : null}
                      {gone ? (
                        <Badge small tone="danger">
                          {t.images.removing}
                        </Badge>
                      ) : null}
                      {editable ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => toggle(image)}
                          aria-label={`${gone ? t.images.undo : t.images.remove}: ${image.FileName}`}
                        >
                          {gone ? t.images.undo : t.images.remove}
                        </Button>
                      ) : null}
                    </li>
                  )
                })}
              {added
                .filter((image) => image.ImageType === imageType)
                .map((image) => (
                  <li key={image.AssetId} className="flex flex-wrap items-center gap-2 text-[13px]">
                    <span className="mono min-w-0 flex-1 truncate">{image.FileName}</span>
                    <Badge small tone="success">
                      {t.images.adding}
                    </Badge>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => setAdded((list) => list.filter((item) => item.AssetId !== image.AssetId))}
                      aria-label={`${t.images.remove}: ${image.FileName}`}
                    >
                      {t.images.remove}
                    </Button>
                  </li>
                ))}
            </ul>
          </section>
        ))}
        {editable && locale ? (
          <section className="card" aria-label={t.images.add}>
            <div className="card-h">
              <h2 className="h3">{t.images.add}</h2>
            </div>
            <div className="card-b grid gap-3 md:grid-cols-2">
              <Field label={t.images.type} error={full ? t.images.full : null}>
                <Select
                  aria-label={t.images.type}
                  value={type}
                  options={IMAGE_TYPES.map((item) => ({ value: item, label: item }))}
                  onChange={setType}
                />
              </Field>
              <Field
                label={t.images.caption}
                counter={{ value: charCount(caption), max: MS_LIMITS.imageCaption }}
                hint={interpolate(t.images.captionHint, { max: MS_LIMITS.imageCaption })}
              >
                <Input value={caption} onChange={(event) => setCaption(event.target.value)} />
              </Field>
              <div className="md:col-span-2">
                {full || captionOver ? null : (
                  <AssetUpload
                    key={`${type}-${added.length}`}
                    purpose={AssetPurpose.Screenshot}
                    accept={['.png']}
                    label={t.images.upload}
                    hint={t.images.uploadHint}
                    onUploaded={(asset) => {
                      setAdded((list) => [
                        ...list,
                        { AssetId: asset.Id, ImageType: type, Description: caption.trim() || null, FileName: asset.FileName },
                      ])
                      setCaption('')
                    }}
                  />
                )}
              </div>
            </div>
          </section>
        ) : null}
      </div>
      {editable && (added.length > 0 || removed.length > 0) ? (
        <div className="card sticky bottom-3 mt-4">
          <div className="card-b flex flex-wrap items-center gap-3">
            <b className="min-w-0 flex-1 text-[13px]">{interpolate(t.images.pending, { added: added.length, removed: removed.length })}</b>
            <Button
              variant="primary"
              loading={plan.pending}
              onClick={() =>
                plan.make({
                  Kind: MsChangeKind.Images,
                  Images: {
                    Locale: locale,
                    Add: added.map((image) => ({ AssetId: image.AssetId, ImageType: image.ImageType, Description: image.Description })),
                    RemoveFileNames: removed,
                  },
                })
              }
            >
              {t.planMake}
            </Button>
          </div>
        </div>
      ) : null}
    </>
  )
}

function ImagesScreen({ context }: { context: MsContext }) {
  const { organizationId, record } = context
  const content = useQuery({
    queryKey: msKeys.content(organizationId, record.StoreAppId, null, null),
    queryFn: () => msApi.content(organizationId, record.StoreAppId),
  })
  if (content.isLoading) {
    return <CardSkeleton />
  }
  if (content.isError || !content.data) {
    return <ScreenState error={content.error} onRetry={() => void content.refetch()} />
  }
  return <Images context={context} content={content.data} />
}

export function MsImagesPage() {
  return <MsWorkspace sub="images">{(context) => <ImagesScreen context={context} />}</MsWorkspace>
}
