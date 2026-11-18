import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { AssetPurpose } from '@/lib/api/types'
import { ASSET_MAX_BYTES, putBlob, UploadAbortedError, uploadAsset, UploadFailedError } from '@/lib/assets/upload'
import { asset, TEST_ORG_ID } from '../../support/factories'
import { endpoint, envelope, STORAGE_URL } from '../../support/handlers'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo } from '../../support/state'

const CORS = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Expose-Headers': 'ETag' }

// The presigned storage: records what each PUT received and answers it with an ETag.
function storage(status = 200) {
  const received: Array<{ path: string; contentType: string | null; size: number }> = []
  server.use(
    http.put(`${STORAGE_URL}/*`, async ({ request }) => {
      const body = await request.arrayBuffer()
      const path = new URL(request.url).pathname.replace('/bucket/', '')
      received.push({ path, contentType: request.headers.get('Content-Type'), size: body.byteLength })
      return new HttpResponse(null, { status, headers: { ...CORS, ETag: `"etag-${path}"` } })
    }),
  )
  return received
}

function png(name = '01-home.png', size = 4) {
  return new File([new Uint8Array(size)], name, { type: 'image/png' })
}

describe('uploadAsset', () => {
  it('declares the file, PUTs it once with its type and confirms it', async () => {
    const received = storage()
    const progress = vi.fn()

    const result = await uploadAsset(TEST_ORG_ID, png(), AssetPurpose.Screenshot, { onProgress: progress })

    expect(result.Id).toBe(501)
    expect(lastRequestTo('/Assets/upload')?.body).toEqual({
      OrganizationId: TEST_ORG_ID,
      Purpose: AssetPurpose.Screenshot,
      FileName: '01-home.png',
      ContentType: 'image/png',
      Size: 4,
    })
    expect(received).toEqual([{ path: 'single', contentType: 'image/png', size: 4 }])
    expect(lastRequestTo('/Assets/asset/complete')?.body).toEqual({ OrganizationId: TEST_ORG_ID, AssetId: 501, Parts: [] })
    expect(progress).toHaveBeenLastCalledWith({ loaded: 4, total: 4 })
  })

  it('sends a large file in parts, in order, and completes with every part ETag', async () => {
    const received = storage()
    server.use(
      endpoint('post', '/Assets/upload', () =>
        HttpResponse.json(
          envelope({
            AssetId: 502,
            UploadUrl: null,
            PartSize: 4,
            Parts: [
              { PartNumber: 2, Url: `${STORAGE_URL}/part-2` },
              { PartNumber: 1, Url: `${STORAGE_URL}/part-1` },
            ],
            ExpiryDate: '2026-11-18T11:00:00Z',
          }),
        ),
      ),
    )

    await uploadAsset(TEST_ORG_ID, png('build.aab', 6), AssetPurpose.BuildArtifact)

    expect(received).toEqual([
      { path: 'part-1', contentType: null, size: 4 },
      { path: 'part-2', contentType: null, size: 2 },
    ])
    expect(lastRequestTo('/Assets/asset/complete')?.body).toEqual({
      OrganizationId: TEST_ORG_ID,
      AssetId: 502,
      Parts: [
        { PartNumber: 1, ETag: '"etag-part-1"' },
        { PartNumber: 2, ETag: '"etag-part-2"' },
      ],
    })
  })

  it('fails without confirming when the storage refuses the bytes', async () => {
    storage(403)

    await expect(uploadAsset(TEST_ORG_ID, png(), AssetPurpose.Screenshot)).rejects.toBeInstanceOf(UploadFailedError)
    expect(lastRequestTo('/Assets/asset/complete')).toBeUndefined()
  })

  it('stops before sending when it was already cancelled', async () => {
    const abort = new AbortController()
    abort.abort()

    await expect(putBlob(`${STORAGE_URL}/single`, png(), 'image/png', undefined, abort.signal)).rejects.toBeInstanceOf(UploadAbortedError)
  })

  it('mirrors the API size limits', () => {
    expect(ASSET_MAX_BYTES[AssetPurpose.Screenshot]).toBe(50 * 1024 * 1024)
    expect(ASSET_MAX_BYTES[AssetPurpose.Graphic]).toBe(20 * 1024 * 1024)
    expect(ASSET_MAX_BYTES[AssetPurpose.BuildArtifact]).toBe(8 * 1024 * 1024 * 1024)
  })
})

describe('AssetUpload', () => {
  function render(onUploaded = vi.fn()) {
    const view = renderWithProviders(
      <AssetUpload
        purpose={AssetPurpose.Screenshot}
        accept={['.png', '.jpg']}
        hint="PNG, JPG"
        label="Ekran görüntüsü"
        onUploaded={onUploaded}
      />,
    )
    return { ...view, onUploaded }
  }

  it('uploads a chosen file and hands over the confirmed asset', async () => {
    storage()
    const { user, onUploaded } = render()

    await user.upload(await screen.findByLabelText('Ekran görüntüsü'), png())

    expect(await screen.findByText('Yüklendi')).toBeInTheDocument()
    expect(onUploaded).toHaveBeenCalledWith(asset())
  })

  it('refuses a file of another type before any request', async () => {
    render()

    // A drop does not go through the picker's accept filter; the zone checks the name itself.
    fireEvent.change(await screen.findByLabelText('Ekran görüntüsü'), {
      target: { files: [new File(['x'], 'kapak.heic', { type: 'image/heic' })] },
    })

    expect(screen.getByRole('alert')).toHaveTextContent('kapak.heic: bu dosya türü kabul edilmiyor.')
    expect(lastRequestTo('/Assets/upload')).toBeUndefined()
  })

  it('says why an upload failed and tries again', async () => {
    storage(500)
    const { user, onUploaded } = render()

    await user.upload(await screen.findByLabelText('Ekran görüntüsü'), png())
    expect(await screen.findByText('Yükleme başarısız')).toBeInTheDocument()
    storage()
    await user.click(screen.getByRole('button', { name: 'Tekrar dene' }))

    await waitFor(() => expect(onUploaded).toHaveBeenCalled())
  })
})
