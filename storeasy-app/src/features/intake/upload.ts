import { putBlob, UploadFailedError, type UploadOptions } from '@/lib/assets/upload'
import { intakeApi } from './api'
import { ArtifactSource, type ArtifactUploadedPartDto } from './types'

// The browser side of a manual package upload (design 13.3, tools/CONTRACT.md 3): the SHA-256 is computed here, the
// session is opened (or the open one of the same file resumed, or a duplicate reported), the missing parts go straight to
// their presigned URLs, and complete starts processing. The bytes never pass through the API.

export async function sha256Hex(file: Blob): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer())
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('')
}

export interface ArtifactUploadResult {
  sessionId: number
  duplicate: boolean
  originalSessionId: number | null
}

export async function uploadArtifact(
  organizationId: number,
  appId: number,
  file: File,
  options: UploadOptions & { notes?: string; hold?: boolean; onHashed?: () => void } = {},
): Promise<ArtifactUploadResult> {
  const { onProgress, signal, notes, hold, onHashed } = options
  const sha256 = await sha256Hex(file)
  onHashed?.()
  const session = await intakeApi.open(organizationId, appId, {
    FileName: file.name,
    Size: file.size,
    Sha256: sha256,
    ContentType: file.type || 'application/octet-stream',
    Source: ArtifactSource.Manual,
    // Held: read and checked, then waiting for a member to deliver it from the session (HoldForConfirmation).
    Delivery:
      notes?.trim() || hold ? { ...(notes?.trim() ? { Notes: notes.trim() } : {}), ...(hold ? { HoldForConfirmation: true } : {}) } : null,
  })
  if (session.Duplicate) {
    return { sessionId: session.SessionId, duplicate: true, originalSessionId: session.OriginalSessionId ?? null }
  }

  const partSize = session.PartSize ?? file.size
  const sizeOf = (partNumber: number) => Math.max(0, Math.min(partSize, file.size - (partNumber - 1) * partSize))
  const parts: ArtifactUploadedPartDto[] = [...(session.UploadedParts ?? [])]
  let done = parts.reduce((sum, part) => sum + sizeOf(part.PartNumber), 0)
  const report = (loaded: number) => onProgress?.({ loaded: Math.min(loaded, file.size), total: file.size })
  report(done)

  for (const part of [...(session.Parts ?? [])].sort((a, b) => a.PartNumber - b.PartNumber)) {
    const start = (part.PartNumber - 1) * partSize
    const slice = file.slice(start, Math.min(start + partSize, file.size))
    const etag = await putBlob(part.Url, slice, null, (loaded) => report(done + loaded), signal)
    if (!etag) {
      // Without the ETag the storage cannot assemble the file; the bucket's CORS must expose it.
      throw new UploadFailedError(0)
    }
    parts.push({ PartNumber: part.PartNumber, ETag: etag })
    done += slice.size
  }

  await intakeApi.complete(
    organizationId,
    session.SessionId,
    parts.sort((a, b) => a.PartNumber - b.PartNumber),
  )
  return { sessionId: session.SessionId, duplicate: false, originalSessionId: null }
}
