import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, StoreKind } from '@/lib/api/types'
import { AppReviewsPage, ReviewsPage, reviewFilter } from '@/features/reviews/ReviewsPage'
import { SavedRepliesPage } from '@/features/reviews/SavedRepliesPage'
import { fillVariables, graphemeCount, repliesFor, unknownVariables, variantFor } from '@/features/reviews/text'
import { ReviewReplyState } from '@/features/reviews/types'
import { activityEvent, organization } from '../../support/factories'
import { review, savedReply, phase4State, setupPhase4Api } from '../../support/phase4'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupPhase4Api()

function renderInbox(route = '/reviews') {
  return renderWithProviders(<ReviewsPage />, { route, path: '/reviews' })
}

describe('review helpers', () => {
  it('reads the address filters as the API query', () => {
    expect(reviewFilter(new URLSearchParams('app=101&store=google&rating=1-2&lang=tr&replied=no&read=no&q=pil'), null)).toEqual({
      AppId: 101,
      Store: StoreKind.Google,
      MinRating: 1,
      MaxRating: 2,
      Locale: 'tr',
      HasReply: false,
      IsRead: false,
      Search: 'pil',
    })
    expect(reviewFilter(new URLSearchParams('rating=5&replied=yes'), 9)).toMatchObject({
      AppId: 9,
      MinRating: 5,
      MaxRating: 5,
      HasReply: true,
    })
  })

  it('counts graphemes, fills variables and finds unknown ones', () => {
    expect(graphemeCount('ağ👍🏽')).toBe(3)
    expect(fillVariables('Merhaba {ad}, {app} {sürüm} {x}', { author: 'Ayşe', app: 'Mola', version: '3.1' })).toBe(
      'Merhaba Ayşe, Mola 3.1 {x}',
    )
    expect(unknownVariables('{ad} {isim} {isim}')).toEqual(['isim'])
  })

  it('offers the saved replies of the app, the review language first', () => {
    const english = savedReply({ Id: 1, Variants: [{ Locale: 'en-US', Text: 'Hi' }] })
    const turkish = savedReply({ Id: 2, Variants: [{ Locale: 'tr', Text: 'Selam' }] })
    const otherApp = savedReply({ Id: 3, AppIds: [999] })

    const offered = repliesFor([english, turkish, otherApp], review())

    expect(offered.map((item) => item.Id)).toEqual([2, 1])
    expect(variantFor(savedReply(), 'en-GB')?.Locale).toBe('en-US')
    expect(variantFor(savedReply({ Variants: [] }), 'tr')).toBeNull()
  })
})

describe('ReviewsPage', () => {
  it('lists the reviews and marks the opened one read', async () => {
    const { user } = renderInbox()

    await user.click(await screen.findByRole('button', { name: /Ayşe K\./ }))

    expect(await screen.findByRole('article', { name: 'Ayşe K. yorumu' })).toHaveTextContent('Pixel 8')
    expect(screen.getByTestId('location')).toHaveTextContent('id=3001')
    expect(lastRequestTo('/Reviews/read')?.body).toMatchObject({ ReviewIds: [3001], IsRead: true })
  })

  it('keeps the filters in the address and sends them', async () => {
    const { user } = renderInbox()
    await screen.findByRole('button', { name: /Ayşe K\./ })

    await user.click(screen.getByRole('button', { name: 'Yanıtsız' }))
    await user.type(screen.getByRole('searchbox', { name: 'Yorum metninde ya da yazarda ara' }), 'hatırlatıcı{Enter}')

    expect(screen.getByTestId('location')).toHaveTextContent('replied=no')
    expect(screen.getByTestId('location')).toHaveTextContent('q=hat')
    expect(lastRequestTo('/Reviews')?.body).toMatchObject({ HasReply: false, Search: 'hatırlatıcı', OrganizationId: 7 })
  })

  it('fills a saved reply with the review values and sends the reply', async () => {
    const { user } = renderInbox('/reviews?id=3001')
    await screen.findByRole('article', { name: 'Ayşe K. yorumu' })

    await user.click(screen.getByRole('button', { name: 'Kayıtlı yanıt' }))
    await user.click(await screen.findByRole('menuitem', { name: /Hata bildirimi için teşekkür/ }))
    expect(screen.getByLabelText(/Google Play sınırı/)).toHaveValue('Merhaba Ayşe K., Pusula 3.1.2 için teşekkürler.')
    await user.click(screen.getByRole('button', { name: 'Yanıtı gönder' }))

    expect(await screen.findByText('Yanıt mağazaya gönderildi.')).toBeInTheDocument()
    expect(lastRequestTo('/Reviews/reply')?.body).toMatchObject({ ReviewId: 3001, SavedReplyId: 41 })
    expect(lastRequestTo('/Reviews/reply')?.headers['idempotency-key']).toBeTruthy()
  })

  it('blocks a reply over the Google Play limit and saves a draft', async () => {
    const { user } = renderInbox('/reviews?id=3001')
    const box = await screen.findByLabelText(/Google Play sınırı/)

    await user.click(box)
    await user.paste('x'.repeat(351))

    expect(screen.getByText('Yanıt bu mağaza için 1 karakter uzun.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Yanıtı gönder' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Taslağı kaydet' }))
    expect(await screen.findByText('Taslak kaydedildi.')).toBeInTheDocument()
    expect(lastRequestTo('/Reviews/reply-draft')?.body).toMatchObject({ ReviewId: 3001 })
  })

  it('shows a published reply and lets an editor change it', async () => {
    const { user } = renderInbox('/reviews?id=3002')

    expect(await screen.findByText('Danke!')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Düzenle' }))

    expect(screen.getByLabelText(/App Store sınırı/)).toHaveValue('Danke!')
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }))
    expect(screen.queryByLabelText(/App Store sınırı/)).not.toBeInTheDocument()
  })

  it('shows the store error of a failed reply with the text kept', async () => {
    phase4State.reviews = [
      review({ ReplyState: ReviewReplyState.Failed, ReplyBody: 'Merhaba', ReplyError: '403 PERMISSION_DENIED', ReplyJobId: 48207 }),
    ]

    renderInbox('/reviews?id=3001')

    expect(await screen.findByText('403 PERMISSION_DENIED')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'İşi aç' })).toHaveAttribute('href', '/activity/jobs/48207')
    expect(screen.getByLabelText(/Google Play sınırı/)).toHaveValue('Merhaba')
  })

  it('shows a Viewer a locked reply area and no sync', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]

    renderInbox('/reviews?id=3001')

    expect(await screen.findByText('Yanıt yazma iznin yok')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Şimdi çek' })).not.toBeInTheDocument()
  })

  it('fetches new reviews now', async () => {
    const { user } = renderInbox()

    await user.click(await screen.findByRole('button', { name: 'Şimdi çek' }))

    expect(await screen.findByText('Yorumlar çekiliyor (2 iş).')).toBeInTheDocument()
    expect(lastRequestTo('/Reviews/sync')?.body).toMatchObject({ AppId: null })
  })

  it('says when nothing is left unanswered and clears the filters', async () => {
    phase4State.reviews = []
    const { user } = renderInbox('/reviews?replied=no')

    expect(await screen.findByRole('heading', { name: 'Yanıtsız yorum kalmadı' })).toBeInTheDocument()
    await user.click(screen.getAllByRole('button', { name: 'Filtreleri temizle' })[0])

    expect(screen.getByTestId('location')).not.toHaveTextContent('replied')
  })

  it('reads the inbox again when new reviews arrive on the hub', async () => {
    const { activity } = renderInbox()
    await screen.findByRole('button', { name: /Ayşe K\./ })
    const before = requestsTo('/Reviews').length

    ;(activity as unknown as { push: (event: unknown) => void }).push(activityEvent({ Type: 'review.received' }))

    await screen.findByRole('button', { name: /Ayşe K\./ })
    await expect.poll(() => requestsTo('/Reviews').length).toBeGreaterThan(before)
  })

  it('serves one app inbox inside the app workspace', async () => {
    renderWithProviders(<AppReviewsPage />, { route: '/apps/101/reviews', path: '/apps/:appId/reviews' })

    await screen.findByRole('button', { name: /Ayşe K\./ })
    expect(lastRequestTo('/Reviews')?.body).toMatchObject({ AppId: 101 })
    expect(screen.queryByRole('button', { name: 'Uygulama' })).not.toBeInTheDocument()
  })
})

describe('SavedRepliesPage', () => {
  it('lists the saved replies with their languages and use count', async () => {
    renderWithProviders(<SavedRepliesPage />)

    expect(await screen.findByText('Hata bildirimi için teşekkür')).toBeInTheDocument()
    expect(screen.getByText('en-US')).toBeInTheDocument()
    expect(screen.getByText('Bütün uygulamalar')).toBeInTheDocument()
  })

  it('warns when a text is too long for Google Play', async () => {
    phase4State.savedReplies = [savedReply({ Variants: [{ Locale: 'en-US', Text: 'y'.repeat(372) }] })]

    renderWithProviders(<SavedRepliesPage />)

    expect(await screen.findByText(/en-US metni 372 karakter/)).toBeInTheDocument()
  })

  it('adds a saved reply, refusing an unknown variable', async () => {
    const { user } = renderWithProviders(<SavedRepliesPage />)
    await user.click(await screen.findByRole('button', { name: 'Yanıt ekle' }))
    const dialog = await screen.findByRole('dialog', { name: 'Yeni kayıtlı yanıt' })

    await user.type(within(dialog).getByLabelText('Ad'), 'Destek')
    await user.type(within(dialog).getByLabelText('Metin (tr)'), 'Merhaba {{isim}')
    expect(within(dialog).getByText(/Bilinmeyen değişken: \{isim\}/)).toBeInTheDocument()
    await user.clear(within(dialog).getByLabelText('Metin (tr)'))
    await user.type(within(dialog).getByLabelText('Metin (tr)'), 'Merhaba ')
    await user.click(within(dialog).getByRole('button', { name: '{ad}' }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Kayıtlı yanıt saklandı.')).toBeInTheDocument()
    expect(lastRequestTo('/Reviews/saved-reply')?.body).toMatchObject({
      Name: 'Destek',
      Variants: [{ Locale: 'tr', Text: 'Merhaba {ad}' }],
      AppIds: [],
    })
  })

  it('edits a reply for chosen apps and adds a language', async () => {
    const { user } = renderWithProviders(<SavedRepliesPage />)
    await user.click(await screen.findByText('Hata bildirimi için teşekkür'))
    const dialog = await screen.findByRole('dialog', { name: 'Kayıtlı yanıtı düzenle' })

    await user.type(within(dialog).getByLabelText('Yeni dil kodu'), 'de-DE')
    await user.click(within(dialog).getByRole('button', { name: 'Dil' }))
    await user.type(within(dialog).getByLabelText('Metin (de-DE)'), 'Hallo')
    await user.click(within(dialog).getByRole('radio', { name: 'Seçilenler' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Pusula' }))
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Kayıtlı yanıt saklandı.')).toBeInTheDocument()
    const body = lastRequestTo('/Reviews/saved-reply')?.body as { SavedReplyId: number; AppIds: number[]; Variants: unknown[] }
    expect(body.SavedReplyId).toBe(41)
    expect(body.AppIds).toEqual([101])
    expect(body.Variants).toHaveLength(3)
  })

  it('deletes a saved reply after a confirmation', async () => {
    const { user } = renderWithProviders(<SavedRepliesPage />)
    await user.click(await screen.findByText('Hata bildirimi için teşekkür'))
    const dialog = await screen.findByRole('dialog', { name: 'Kayıtlı yanıtı düzenle' })

    await user.click(within(dialog).getByRole('button', { name: 'Sil' }))
    const confirm = await screen.findByRole('alertdialog')
    await user.click(within(confirm).getByRole('button', { name: 'Sil' }))

    expect(await screen.findByText('Kayıtlı yanıt silindi.')).toBeInTheDocument()
    expect(lastRequestTo('/Reviews/saved-reply')?.method).toBe('DELETE')
  })

  it('is read only for a Viewer and empty without replies', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    phase4State.savedReplies = []

    renderWithProviders(<SavedRepliesPage />)

    expect(await screen.findByRole('heading', { name: 'Henüz kayıtlı yanıt yok' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yanıt ekle' })).not.toBeInTheDocument()
  })
})
