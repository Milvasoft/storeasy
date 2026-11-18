import { assetsApi } from '@/lib/api/endpoints'
import { AssetPurpose, type AssetDto, type UploadedPart } from '@/lib/api/types'

// Uploading a file to the organization's storage (design 19 file drop; API AssetsController): declare it, PUT the bytes
// straight to the presigned URL(s), then confirm. The bytes never pass through the API.

const MB = 1024 * 1024
const GB = 1024 * MB

/** The API's size limits (Storeasy.Domain AssetRules.MaxBytes), checked in the browser before anything is sent. */
export const ASSET_MAX_BYTES: Record<AssetPurpose, number> = {
  [AssetPurpose.Screenshot]: 50 * MB,
  [AssetPurpose.Graphic]: 20 * MB,
  [AssetPurpose.MockupOutput]: 50 * MB,
  [AssetPurpose.BuildArtifact]: 8 * GB,
}

export class UploadAbortedError extends Error {
  constructor() {
    super('The upload was cancelled.')
    this.name = 'UploadAbortedError'
  }
}

export class UploadFailedError extends Error {
  status: number
  constructor(status: number) {
    super(`The storage refused the upload (${status}).`)
    this.name = 'UploadFailedError'
    this.status = status
  }
}

export interface UploadProgress {
  loaded: number
  total: number
}

export interface UploadOptions {
  onProgress?: (progress: UploadProgress) => void
  signal?: AbortSignal
}

/**
 * PUTs one body to a presigned URL with XMLHttpRequest (fetch has no upload progress) and answers the ETag of the
 * response, which a multipart upload needs for the complete call. No Authorization header: the URL is the credential.
 */
export function putBlob(
  url: string,
  body: Blob,
  contentType: string | null,
  onProgress?: (loaded: number) => void,
  signal?: AbortSignal,
): Promise<string | null> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new UploadAbortedError())
      return
    }
    const xhr = new XMLHttpRequest()
    const abort = () => xhr.abort()
    signal?.addEventListener('abort', abort, { once: true })
    xhr.open('PUT', url)
    if (contentType) {
      xhr.setRequestHeader('Content-Type', contentType)
    }
    xhr.upload.onprogress = (event) => onProgress?.(event.loaded)
    xhr.onload = () => {
      signal?.removeEventListener('abort', abort)
      if (xhr.status >= 200 && xhr.status < 300) {
        onProgress?.(body.size)
        resolve(xhr.getResponseHeader('ETag'))
      } else {
        reject(new UploadFailedError(xhr.status))
      }
    }
    xhr.onerror = () => {
      signal?.removeEventListener('abort', abort)
      reject(new UploadFailedError(0))
    }
    xhr.onabort = () => reject(new UploadAbortedError())
    xhr.send(body)
  })
}

/**
 * Uploads `file` for `purpose` and answers the confirmed asset. A file over 64 MB goes in parts, one after the other,
 * each part's ETag kept for the complete call. Progress is reported in bytes over the whole file.
 */
export async function uploadAsset(
  organizationId: number,
  file: File,
  purpose: AssetPurpose,
  options: UploadOptions = {},
): Promise<AssetDto> {
  const { onProgress, signal } = options
  const contentType = file.type || 'application/octet-stream'
  const upload = await assetsApi.createUpload(organizationId, {
    Purpose: purpose,
    FileName: file.name,
    ContentType: contentType,
    Size: file.size,
  })
  const report = (loaded: number) => onProgress?.({ loaded: Math.min(loaded, file.size), total: file.size })
  report(0)

  const parts: UploadedPart[] = []
  if (upload.UploadUrl) {
    await putBlob(upload.UploadUrl, file, contentType, report, signal)
  } else {
    const partSize = upload.PartSize ?? file.size
    let done = 0
    for (const part of [...(upload.Parts ?? [])].sort((a, b) => a.PartNumber - b.PartNumber)) {
      const start = (part.PartNumber - 1) * partSize
      const slice = file.slice(start, Math.min(start + partSize, file.size))
      // Parts carry no Content-Type: it was fixed when the multipart upload was created.
      const etag = await putBlob(part.Url, slice, null, (loaded) => report(done + loaded), signal)
      if (!etag) {
        // Without the ETag the storage cannot assemble the file; the bucket's CORS must expose it.
        throw new UploadFailedError(0)
      }
      parts.push({ PartNumber: part.PartNumber, ETag: etag })
      done += slice.size
    }
  }
  return assetsApi.complete(organizationId, upload.AssetId, parts)
}
