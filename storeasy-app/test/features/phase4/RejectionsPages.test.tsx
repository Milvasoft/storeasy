import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { RejectionCasePage, RejectionsPage, replyLength } from '@/features/rejections/RejectionsPages'
import { ChecklistItemStatus, RejectionCloseReason, RejectionKind, RejectionState } from '@/features/rejections/types'
import { activityEvent, organization } from '../../support/factories'
import { phase4State, rejectionCase, setupPhase4Api } from '../../support/phase4'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupPhase4Api()

function renderCase() {
  return renderWithProviders(<RejectionCasePage />, { route: '/apps/101/rejections/601', path: '/apps/:appId/rejections/:caseId' })
}

describe('RejectionsPage', () => {
  it('lists the open cases and the submission history', async () => {
    renderWithProviders(<RejectionsPage />, { route: '/apps/101/rejections', path: '/apps/:appId/rejections' })

    expect(await screen.findByRole('link', { name: '1.4 (52)' })).toHaveAttribute('href', '/apps/101/rejections/601')
    expect(screen.getByText('2.1 · 5.1.1')).toBeInTheDocument()
    expect(screen.getByText('1 / 4')).toBeInTheDocument()
    expect(await screen.findByText(/2 gönderimde 1 red\./)).toBeInTheDocument()
    expect(lastRequestTo('/Rejections')?.body).toMatchObject({ AppId: 101, State: RejectionState.Open })
  })

  it('lists every case and refreshes when a case is opened on the hub', async () => {
    const { user, activity } = renderWithProviders(<RejectionsPage />, { route: '/apps/101/rejections', path: '/apps/:appId/rejections' })
    await screen.findByRole('link', { name: '1.4 (52)' })

    await user.click(screen.getByRole('button', { name: 'Tümü' }))
    await expect.poll(() => (lastRequestTo('/Rejections')?.body as { State: unknown }).State).toBeNull()
    const before = requestsTo('/Rejections').length
    ;(activity as unknown as { push: (event: unknown) => void }).push(activityEvent({ Type: 'rejection.opened' }))

    await expect.poll(() => requestsTo('/Rejections').length).toBeGreaterThan(before)
  })

  it('opens a case by hand and goes to it', async () => {
    const { user } = renderWithProviders(<RejectionsPage />, { route: '/apps/101/rejections', path: '/apps/:appId/rejections' })

    await user.click(await screen.findByRole('button', { name: 'Kayıt aç' }))
    const dialog = await screen.findByRole('dialog', { name: 'Red kaydı aç' })
    await user.type(within(dialog).getByLabelText('Sürüm'), '1.5')
    await user.type(within(dialog).getByLabelText(/Build/), '60')
    await user.click(within(dialog).getByRole('button', { name: 'Kaydı aç' }))

    await expect.poll(() => screen.getByTestId('location').textContent).toBe('/apps/101/rejections/602')
    expect(lastRequestTo('/Rejections/case')?.body).toMatchObject({ AppId: 101, VersionString: '1.5', BuildNumber: '60', Kind: RejectionKind.Rejected })
  })

  it('says when the app has no rejection', async () => {
    phase4State.rejections = []

    renderWithProviders(<RejectionsPage />, { route: '/apps/101/rejections', path: '/apps/:appId/rejections' })

    expect(await screen.findByRole('heading', { name: 'Red kaydı yok' })).toBeInTheDocument()
  })
})

describe('RejectionCasePage', () => {
  it('counts a reply like Resolution Center', () => {
    expect(replyLength('a\r\nb')).toBe(3)
  })

  it('shows the text, the checklist, the draft and the timeline', async () => {
    renderCase()

    expect(await screen.findByRole('heading', { name: 'Pusula 1.4 (52) reddedildi' })).toBeInTheDocument()
    expect(screen.getByText(/We were unable to sign in/)).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Düzeltme listesi 1 / 2' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Düzelt' })).toHaveAttribute('href', '/apps/101/release/review-info')
    expect(screen.getByRole('link', { name: /Asistanlı adımla gönder/ })).toHaveAttribute('href', '/apps/101/assisted/5590')
    expect(screen.getByText('Webhook geldi: REJECTED')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Resolution Center'ı aç/ })).toHaveAttribute('href', phase4State.rejection.ResolutionCenterUrl)
  })

  it('pastes the text and says when no guideline was found', async () => {
    phase4State.rejection = rejectionCase({ RawText: undefined, Guidelines: [], Checklist: [] })
    const { user } = renderCase()

    const box = await screen.findByLabelText('Red metnini buraya yapıştır')
    await user.click(box)
    await user.paste("There's an issue with your submission.")
    await user.click(screen.getByRole('button', { name: 'Kaydet ve ayrıştır' }))

    expect(await screen.findByText('Metin kaydedildi; içinde guideline numarası bulunamadı.')).toBeInTheDocument()
    expect(lastRequestTo('/Rejections/case/text')?.body).toMatchObject({ CaseId: 601, RawText: "There's an issue with your submission." })
  })

  it('warns about a text without guidelines', async () => {
    phase4State.rejection = rejectionCase({ Guidelines: [], RawText: 'Hello' })

    renderCase()

    expect(await screen.findByText('Metinde guideline numarası bulunamadı')).toBeInTheDocument()
  })

  it('changes an item state and adds an item', async () => {
    const { user } = renderCase()
    await screen.findByRole('heading', { name: 'Pusula 1.4 (52) reddedildi' })

    await user.click(screen.getByRole('button', { name: '"İnceleme notunu güncelle" durumu' }))
    await user.click(await screen.findByRole('option', { name: 'Yapıldı' }))
    await expect.poll(() => requestsTo('/Rejections/case/checklist').length).toBe(1)
    expect(lastRequestTo('/Rejections/case/checklist')?.body).toMatchObject({
      Items: [{ Id: 'c1' }, { Id: 'c2', Status: ChecklistItemStatus.Done }],
    })
    await user.type(screen.getByLabelText('Yeni madde'), 'Hesap silme ekle')
    await user.type(screen.getByLabelText(/Guideline/), '5.1.1')
    await user.click(screen.getByRole('button', { name: 'Madde ekle' }))

    await expect.poll(() => requestsTo('/Rejections/case/checklist').length).toBe(2)
    expect((lastRequestTo('/Rejections/case/checklist')?.body as { Items: unknown[] }).Items[2]).toMatchObject({
      GuidelineCode: '5.1.1',
      Text: 'Hesap silme ekle',
      Status: ChecklistItemStatus.Todo,
    })
  })

  it('blocks marking the reply sent over 4,000 characters', async () => {
    const { user } = renderCase()
    const box = await screen.findByLabelText('Yanıt taslağı')

    await user.click(box)
    await user.paste('z'.repeat(4000))

    expect(screen.getByText(/Resolution Center 4\.000 karakterden uzun yanıtı kabul etmez\. \d+ karakter kısalt\./)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Gönderdim olarak işaretle' })).toBeDisabled()
    expect(screen.getByRole('button', { name: /Asistanlı adımla gönder/ })).toBeDisabled()
  })

  it('saves a changed draft before marking it sent', async () => {
    const { user } = renderCase()
    const box = await screen.findByLabelText('Yanıt taslağı')

    await user.type(box, ' Thanks.')
    await user.click(screen.getByRole('button', { name: 'Gönderdim olarak işaretle' }))

    expect(await screen.findByText('Yanıt gönderildi olarak işaretlendi.')).toBeInTheDocument()
    expect(requestsTo('/Rejections/case/reply-draft')).toHaveLength(1)
    expect(lastRequestTo('/Rejections/case/reply-sent')?.body).toMatchObject({ CaseId: 601 })
  })

  it('saves the draft on its own', async () => {
    const { user } = renderCase()
    const box = await screen.findByLabelText('Yanıt taslağı')

    await user.type(box, '!')
    await user.click(screen.getByRole('button', { name: 'Taslağı kaydet' }))

    expect(await screen.findByText('Taslak kaydedildi.')).toBeInTheDocument()
    expect((lastRequestTo('/Rejections/case/reply-draft')?.body as { Body: string }).Body.endsWith('!')).toBe(true)
  })

  it('closes the case with a note', async () => {
    const { user } = renderCase()

    await user.click(await screen.findByRole('button', { name: 'Kaydı kapat' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Bu kayıt kapatılsın mı?' })
    await user.type(within(dialog).getByLabelText(/Not/), 'Sürüm geri çekildi.')
    await user.click(within(dialog).getByRole('button', { name: 'Kaydı kapat' }))

    expect(await screen.findByText('Kayıt kapatıldı.')).toBeInTheDocument()
    expect(lastRequestTo('/Rejections/case/close')?.body).toMatchObject({ CaseId: 601, Note: 'Sürüm geri çekildi.' })
  })

  it('shows a closed case read only with its sent reply', async () => {
    phase4State.rejection = rejectionCase({
      State: RejectionState.Closed,
      ClosedVia: RejectionCloseReason.Submission,
      ClosedDate: '2026-11-20T10:00:00Z',
      RepeatedGuidelines: ['2.1'],
      ReplyDraft: { ...rejectionCase().ReplyDraft!, SentDate: '2026-11-18T12:00:00Z', SentByUserName: 'deniz@kuzeylab.dev' },
    })

    renderCase()

    expect(await screen.findByText('Bu kayıt geçmişte; düzenlenemez.')).toBeInTheDocument()
    expect(screen.getByText('Yanıt gönderildi')).toBeInTheDocument()
    expect(screen.getByText('Aynı guideline yeniden geldi')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kaydı kapat' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Yanıt taslağı')).not.toBeInTheDocument()
  })

  it('shows a Viewer the waiting state without a text', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    phase4State.rejection = rejectionCase({ RawText: undefined, Kind: RejectionKind.CertificationFailed })

    renderCase()

    expect(await screen.findByText(/Red metni bekleniyor/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Pusula 1.4 (52) sertifikasyondan geçmedi' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kaydı kapat' })).not.toBeInTheDocument()
  })
})
