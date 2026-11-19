import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole, PreReviewArea, PreReviewSeverity, PreReviewStatus, PreReviewVerdict, StoreKind } from '@/lib/api/types'
import { PreReviewPage } from '@/features/pre-review/PreReviewPage'
import { ReadinessPage } from '@/features/readiness/ReadinessPage'
import { formatCost } from '@/features/pre-review/PreReviewPage'
import { setupContentApi } from '../../support/content'
import { organization } from '../../support/factories'
import { preReview } from '../../support/preReview'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, testState } from '../../support/state'

setupContentApi()

function render(query = '?store=apple') {
  return renderWithProviders(<PreReviewPage />, { route: `/apps/101/pre-review${query}`, path: '/apps/:appId/pre-review' })
}

const older = preReview({
  Id: 8800,
  Verdict: PreReviewVerdict.LikelyRejected,
  ApprovalLikelihood: 20,
  Summary: 'Eski rapor: gizlilik politikası eksik.',
  Findings: [
    {
      Severity: PreReviewSeverity.Blocker,
      Area: PreReviewArea.Privacy,
      Guideline: 'App Store Review Guideline 5.1.1',
      Title: 'Gizlilik politikası yok',
      Evidence: 'privacyPolicyUrl boş',
      Fix: 'Gizlilik politikası adresi ekle.',
    },
  ],
  CreatedDate: '2026-11-10T08:00:00Z',
})

describe('PreReviewPage', () => {
  it('shows the latest report: verdict, likelihood, findings by severity with guidelines, runtime checks and usage', async () => {
    testState.preReviews = [preReview(), older]
    render()

    expect(await screen.findByText('%62 geçme olasılığı')).toBeInTheDocument()
    const summary = screen.getByRole('region', { name: 'Özet' })
    expect(within(summary).getByText('Riskli')).toBeInTheDocument()
    expect(within(summary).getByRole('meter', { name: 'Onay olasılığı' })).toHaveAttribute('aria-valuenow', '62')

    const blockers = screen.getByRole('region', { name: 'Engeller' })
    expect(within(blockers).getByText('Demo hesabı yok')).toBeInTheDocument()
    expect(within(blockers).getByText('App Store Review Guideline 2.1')).toBeInTheDocument()
    expect(within(blockers).getByText(/çalışan bir demo hesabı ekle/)).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Önemli' })).getByText('App Store Review Guideline 2.3.7')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Küçük' })).getByText('App Store Review Guideline 2.3.3')).toBeInTheDocument()

    const runtime = screen.getByRole('region', { name: 'Bir incelemecinin çalışırken test edeceği şeyler' })
    expect(within(runtime).getByText(/Konum izni reddedildiğinde/)).toBeInTheDocument()
    expect(screen.getByText(/yapay zeka tahminidir/)).toBeInTheDocument()
    expect(screen.getByText(/yaklaşık \$0\.0574/)).toBeInTheDocument()
    expect(screen.getByText('Model: Claude Sonnet 5.5')).toBeInTheDocument()
    expect(lastRequestTo('/pre-reviews')?.query).toMatchObject({ AppId: '101', Store: String(StoreKind.Apple) })
  })

  it('lists the history and opens an older report', async () => {
    testState.preReviews = [preReview(), older]
    const { user } = render()

    const history = await screen.findByRole('region', { name: 'Geçmiş' })
    const rows = within(history).getAllByRole('button')
    expect(rows).toHaveLength(2)
    expect(rows[0]).toHaveAttribute('aria-current', 'true')
    expect(within(rows[0]).getByText('1 engel, 1 önemli, 1 küçük')).toBeInTheDocument()
    expect(within(rows[1]).getByText('Büyük olasılıkla reddedilir')).toBeInTheDocument()

    await user.click(rows[1])

    expect(await screen.findByText('Eski rapor: gizlilik politikası eksik.')).toBeInTheDocument()
    expect(screen.getByText('%20 geçme olasılığı')).toBeInTheDocument()
    expect(lastRequestTo('/pre-reviews/pre-review')?.query).toMatchObject({ Id: '8800' })
  })

  it('runs a pre-review and shows the result when the job is done', async () => {
    const { user } = render()

    expect(await screen.findByText(/Henüz ön inceleme yok/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Ön inceleme yap' }))

    expect(await screen.findByText('%88 geçme olasılığı', {}, { timeout: 5000 })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Özet' })).getByText('Büyük olasılıkla geçer')).toBeInTheDocument()
    expect(screen.getByText('Sorun bulunmadı.')).toBeInTheDocument()
    expect(lastRequestTo('/pre-reviews/pre-review')).toBeDefined()
    const started = testState.requests.find((request) => request.method === 'POST' && request.path === '/pre-reviews/pre-review')
    expect(started?.body).toEqual({ OrganizationId: 7, AppId: 101, Store: StoreKind.Apple })
  })

  it('shows a queued pre-review as running and keeps the button off', async () => {
    testState.preReviews = [preReview({ Status: PreReviewStatus.Queued, Verdict: null, ApprovalLikelihood: null, Findings: [] })]
    render()

    expect(await screen.findByText('Başlaması bekleniyor...')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'İnceleniyor...' })).toBeDisabled()
  })

  it('shows the error of a failed pre-review', async () => {
    testState.preReviews = [
      preReview({
        Status: PreReviewStatus.Failed,
        Verdict: null,
        ErrorKey: 'AiRateLimited',
        ErrorMessage: 'Anthropic hız sınırına ulaşıldı.',
      }),
    ]
    render()

    expect(await screen.findByText('Ön inceleme başarısız oldu.')).toBeInTheDocument()
    expect(screen.getByText('Anthropic hız sınırına ulaşıldı.')).toBeInTheDocument()
  })

  it('is off without a key and links to the AI settings', async () => {
    testState.aiSettings = { HasApiKey: false, Model: 'claude-haiku-5-5' }
    render()

    expect(await screen.findByRole('link', { name: 'Yapay zeka ayarları' })).toHaveAttribute('href', '/settings#ai')
    expect(screen.getByRole('button', { name: 'Ön inceleme yap' })).toBeDisabled()
  })

  it('is off for a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    render()

    expect(await screen.findByText('Ön inceleme için Editor ya da üstü rol gerekir.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ön inceleme yap' })).toBeDisabled()
  })
})

describe('pre-review entry in the submission step', () => {
  it('links the readiness gate to the pre-review of the store with the latest verdict', async () => {
    testState.preReviews = [preReview()]
    renderWithProviders(<ReadinessPage />, { route: '/apps/101/readiness?store=apple', path: '/apps/:appId/readiness' })

    expect(await screen.findByRole('link', { name: 'Göndermeden önce ön inceleme yap' })).toHaveAttribute(
      'href',
      '/apps/101/pre-review?store=apple',
    )
    expect(await screen.findByText('Son ön inceleme: Riskli, %62')).toBeInTheDocument()
  })
})

describe('formatCost', () => {
  it('keeps cents of a cent visible', () => {
    expect(formatCost(0.0574)).toBe('0.0574')
    expect(formatCost(1.234)).toBe('1.23')
    expect(formatCost(null)).toBe('0.0000')
  })
})
