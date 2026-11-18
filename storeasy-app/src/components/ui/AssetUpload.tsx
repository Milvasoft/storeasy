import { useEffect, useRef, useState } from 'react'
import { CircleCheck } from 'lucide-react'
import { apiErrorMessage } from '@/lib/api/http'
import type { AssetDto, AssetPurpose } from '@/lib/api/types'
import { ASSET_MAX_BYTES, UploadAbortedError, uploadAsset } from '@/lib/assets/upload'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { FileDrop } from './FileDrop'
import { Progress } from './Progress'
import { Button } from './primitives'

type UploadState =
  | { kind: 'idle'; error: string | null }
  | { kind: 'uploading'; file: File; loaded: number; total: number }
  | { kind: 'done'; asset: AssetDto }
  | { kind: 'failed'; file: File; message: string }

/**
 * A file drop that uploads to the organization's storage (design 19 file drop): type and size checked in the browser,
 * then the presigned upload with live progress and "cancel", then the confirmed asset handed to the caller. A refused
 * file and a failed upload say why; a failed one can be tried again.
 */
export function AssetUpload({
  purpose,
  accept,
  hint,
  label,
  onUploaded,
  maxBytes = ASSET_MAX_BYTES[purpose],
}: {
  purpose: AssetPurpose
  accept: string[]
  hint: string
  label: string
  onUploaded: (asset: AssetDto) => void
  maxBytes?: number
}) {
  const t = useT()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const [state, setState] = useState<UploadState>({ kind: 'idle', error: null })
  const controller = useRef<AbortController | null>(null)

  // Leaving the screen stops a running upload; the unconfirmed asset is cleaned up by the API.
  useEffect(() => () => controller.current?.abort(), [])

  const start = async (file: File) => {
    const abort = new AbortController()
    controller.current = abort
    setState({ kind: 'uploading', file, loaded: 0, total: file.size })
    try {
      const asset = await uploadAsset(organization.Id, file, purpose, {
        signal: abort.signal,
        onProgress: ({ loaded, total }) => setState({ kind: 'uploading', file, loaded, total }),
      })
      setState({ kind: 'done', asset })
      onUploaded(asset)
    } catch (error) {
      if (error instanceof UploadAbortedError) {
        setState({ kind: 'idle', error: null })
      } else {
        setState({ kind: 'failed', file, message: apiErrorMessage(error, t.upload.failed) })
      }
    } finally {
      controller.current = null
    }
  }

  if (state.kind === 'uploading') {
    return (
      <div className="drop up" role="status" aria-label={t.upload.uploading}>
        <div className="flex min-w-0 justify-between gap-3 text-[13px]">
          <b className="truncate">{state.file.name}</b>
          <span className="mono flex-none text-fg2">
            {interpolate(t.upload.progress, { done: format.bytes(state.loaded), total: format.bytes(state.total) })}
          </span>
        </div>
        <Progress value={state.loaded} max={state.total || 1} label={state.file.name} />
        <span className="text-xs text-fg3">
          {t.upload.uploading} ·{' '}
          <button type="button" className="lnk" onClick={() => controller.current?.abort()}>
            {t.upload.cancel}
          </button>
        </span>
      </div>
    )
  }

  if (state.kind === 'done') {
    return (
      <div className="drop up">
        <span className="flex min-w-0 items-center gap-2 text-[13px]">
          <CircleCheck className="h-4 w-4 flex-none text-success" aria-hidden />
          <b className="truncate">{state.asset.FileName}</b>
          <span className="mono ml-auto flex-none text-fg2">{format.bytes(state.asset.Size)}</span>
        </span>
        <span className="text-xs text-fg3">{t.upload.done}</span>
      </div>
    )
  }

  if (state.kind === 'failed') {
    return (
      <div className="drop rej" role="alert">
        <b>{t.upload.failed}</b>
        <span className="err-t">
          {state.file.name} · {state.message}
        </span>
        <span className="flex gap-2">
          <Button size="sm" onClick={() => void start(state.file)}>
            {t.upload.retry}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setState({ kind: 'idle', error: null })}>
            {t.common.cancel}
          </Button>
        </span>
      </div>
    )
  }

  return (
    <FileDrop
      label={label}
      accept={accept}
      maxBytes={maxBytes}
      hint={hint}
      error={state.error}
      onFile={(file) => void start(file)}
      onReject={(file, reason) =>
        setState({
          kind: 'idle',
          error:
            reason === 'type'
              ? interpolate(t.upload.rejectedType, { name: file.name })
              : interpolate(t.upload.rejectedSize, { name: file.name, max: format.bytes(maxBytes) }),
        })
      }
    />
  )
}
