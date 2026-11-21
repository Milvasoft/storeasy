import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { Select } from '@/components/ui/Select'
import { IconButton } from '@/components/ui/primitives'
import { MediaKind } from '@/features/common/types'
import { assetsApi } from '@/lib/api/endpoints'
import { AssetPurpose } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { useExperimentsText } from './i18n'
import type { AsoVariantMediaDto } from './types'

// The images of one variant in one language: sets by store class, each a list of uploaded assets (or a mockup export's
// outputs). The files are storeasy assets; they go to the store only with an approved plan.

export interface MediaClassOption {
  kind: MediaKind
  classKey: string
  label: string
}

/** A small preview of an uploaded asset (a short lived download address). */
export function AssetThumb({ organizationId, assetId, label }: { organizationId: number; assetId: number; label: string }) {
  const url = useQuery({
    queryKey: ['asset-url', organizationId, assetId],
    queryFn: () => assetsApi.downloadUrl(organizationId, assetId),
    staleTime: 5 * 60 * 1000,
  })
  return (
    <span className="inline-flex h-20 w-12 items-center justify-center overflow-hidden rounded border border-line bg-bg2 text-[10px] text-fg3">
      {url.data?.Url ? <img src={url.data.Url} alt={label} className="h-full w-full object-cover" /> : `#${assetId}`}
    </span>
  )
}

function classKeyOf(media: AsoVariantMediaDto): string {
  return `${media.Kind}:${media.ClassKey}`
}

export function VariantMedia({
  organizationId,
  value,
  classes,
  disabled,
  onChange,
}: {
  organizationId: number
  value: AsoVariantMediaDto[]
  classes: MediaClassOption[]
  disabled?: boolean
  onChange: (value: AsoVariantMediaDto[]) => void
}) {
  const t = useExperimentsText()
  const [uploadKey, setUploadKey] = useState(0)
  const used = new Set(value.map(classKeyOf))
  const free = classes.filter((option) => !used.has(`${option.kind}:${option.classKey}`))
  const labelOf = (media: AsoVariantMediaDto) =>
    classes.find((option) => option.kind === media.Kind && option.classKey === media.ClassKey)?.label ?? media.ClassKey

  const update = (index: number, next: AsoVariantMediaDto | null) =>
    onChange(next ? value.map((item, i) => (i === index ? next : item)) : value.filter((_, i) => i !== index))

  return (
    <div className="grid gap-3">
      {value.map((media, index) => (
        <section key={classKeyOf(media)} className="rounded border border-line p-3" aria-label={labelOf(media)}>
          <div className="mb-2 flex items-center gap-2">
            <b className="flex-1 text-[13px]">{labelOf(media)}</b>
            {!disabled ? (
              <IconButton label={interpolate(t.wizard.removeClass, { class: labelOf(media) })} onClick={() => update(index, null)}>
                <X className="h-4 w-4" aria-hidden />
              </IconButton>
            ) : null}
          </div>
          <ul className="m-0 flex list-none flex-wrap gap-2 p-0">
            {media.AssetIds.map((assetId) => (
              <li key={assetId} className="relative">
                <AssetThumb organizationId={organizationId} assetId={assetId} label={`${labelOf(media)} #${assetId}`} />
                {!disabled ? (
                  <button
                    type="button"
                    className="absolute -right-1 -top-1 rounded-full bg-bg1 text-fg2"
                    aria-label={t.wizard.removeAsset}
                    onClick={() => update(index, { ...media, AssetIds: media.AssetIds.filter((id) => id !== assetId) })}
                  >
                    <X className="h-3 w-3" aria-hidden />
                  </button>
                ) : null}
              </li>
            ))}
          </ul>
          {!disabled ? (
            <div className="mt-2">
              <AssetUpload
                key={uploadKey}
                purpose={AssetPurpose.Screenshot}
                accept={media.Kind === MediaKind.Preview ? ['.mov', '.mp4', '.m4v'] : ['.png', '.jpg', '.jpeg']}
                hint={t.wizard.uploadHint}
                label={t.wizard.upload}
                onUploaded={(asset) => {
                  update(index, { ...media, AssetIds: [...media.AssetIds, asset.Id] })
                  setUploadKey((key) => key + 1)
                }}
              />
            </div>
          ) : null}
        </section>
      ))}
      {!disabled && free.length > 0 ? (
        <Select<string>
          value={null}
          placeholder={t.wizard.addClass}
          aria-label={t.wizard.addClass}
          options={free.map((option) => ({ value: `${option.kind}:${option.classKey}`, label: option.label }))}
          onChange={(picked) => {
            const option = free.find((item) => `${item.kind}:${item.classKey}` === picked)
            if (option) {
              onChange([...value, { Kind: option.kind, ClassKey: option.classKey, AssetIds: [] }])
            }
          }}
        />
      ) : null}
    </div>
  )
}
