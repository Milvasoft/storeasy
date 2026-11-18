// SPDX-License-Identifier: AGPL-3.0-only
import { readFile, stat } from 'node:fs/promises'
import { basename } from 'node:path'
import { createHash } from 'node:crypto'
import { StoreasyError, errorMessage } from './errors.js'
import { type ApiClient, causeOf, retryDelay } from './http.js'
import { stripQuery } from './redact.js'
import { type PartUrl, type UploadedPart, contentTypeOf } from './upload.js'

/** AssetUploadDto of the existing asset endpoints (CONTRACT 4.1). */
interface AssetUploadDto {
  assetId: number
  uploadUrl?: string | null
  partSize?: number | null
  parts?: PartUrl[]
  expiryDate?: string
}

const IMAGE_TYPES: Record<string, string> = {
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.gif': 'image/gif',
  '.mp4': 'video/mp4',
  '.mov': 'video/quicktime',
  '.json': 'application/json',
  '.txt': 'text/plain',
  '.pdf': 'application/pdf',
}

export function assetContentType(fileName: string): string {
  const m = /\.[A-Za-z0-9]+$/.exec(fileName)
  return (m && IMAGE_TYPES[m[0].toLowerCase()]) ?? contentTypeOf(fileName)
}

/** PUT bytes to a presigned URL with retries; returns the ETag. */
export async function putWithRetry(api: ApiClient, url: string, body: Uint8Array, attempts = 4): Promise<string> {
  let last = ''
  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      const res = await api.fetch(url, { method: 'PUT', body })
      await res.body?.cancel().catch(() => undefined)
      if (res.ok) {
        return res.headers.get('etag') ?? ''
      }
      last = `HTTP ${res.status}`
      if (res.status >= 400 && res.status < 500 && res.status !== 408 && res.status !== 429) {
        break
      }
      await api.sleep(retryDelay(res, api.retryPolicy.baseDelayMs, attempt))
    } catch (err) {
      last = errorMessage(causeOf(err))
      await api.sleep(api.retryPolicy.baseDelayMs * 2 ** attempt)
    }
  }
  throw new StoreasyError(`Upload to storage failed (${last}, ${stripQuery(url)}).`)
}

/**
 * Uploads one local file as an organization asset (POST assets/upload, presigned PUTs, POST assets/asset/complete)
 * and returns its id. Files are small (screenshots, graphics), so the whole file is read at once.
 */
export async function uploadAsset(api: ApiClient, filePath: string, purpose: number): Promise<number> {
  const st = await stat(filePath).catch(() => undefined)
  if (!st?.isFile()) {
    throw new StoreasyError(`File not found: ${filePath}`)
  }
  const bytes = await readFile(filePath)
  const fileName = basename(filePath)
  const sha256 = createHash('sha256').update(bytes).digest('hex')
  const up = await api.request<AssetUploadDto>('POST', '/api/v1.0/assets/upload', {
    body: { purpose, fileName, contentType: assetContentType(fileName), size: bytes.length, sha256 },
  })
  const parts: UploadedPart[] = []
  if (up.uploadUrl) {
    await putWithRetry(api, up.uploadUrl, bytes)
  } else {
    const partSize = up.partSize ?? 0
    if (!partSize || !up.parts?.length) {
      throw new StoreasyError(`The server sent no upload URL for ${fileName}.`)
    }
    for (const p of [...up.parts].sort((a, b) => a.partNumber - b.partNumber)) {
      const start = (p.partNumber - 1) * partSize
      const eTag = await putWithRetry(api, p.url, bytes.subarray(start, Math.min(start + partSize, bytes.length)))
      parts.push({ partNumber: p.partNumber, eTag })
    }
  }
  await api.request('POST', '/api/v1.0/assets/asset/complete', { body: { assetId: up.assetId, parts }, retry: true })
  return up.assetId
}
