import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserType } from '@/lib/api/types'
import { RuleProposalPage, RuleWatcherPage, excerptDiff } from '@/features/rules-watcher/RuleWatcherPages'
import { RuleProposalRejectReason, RuleProposalStatus } from '@/features/rules-watcher/types'
import { endpoint, errorResponse } from '../../support/handlers'
import { phase4State, ruleProposal, setupPhase4Api } from '../../support/phase4'
import { renderWithProviders } from '../../support/render'
import { server } from '../../support/server'
import { lastRequestTo } from '../../support/state'

setupPhase4Api()

const MANAGER = { userType: UserType.Manager }

function renderProposal() {
  return renderWithProviders(<RuleProposalPage />, {
    ...MANAGER,
    route: '/admin/rules/proposals/14',
    path: '/admin/rules/proposals/:proposalId',
  })
}

describe('excerptDiff', () => {
  it('marks removed, added and kept lines', () => {
    expect(excerptDiff('a\nb', 'a\nc')).toEqual([
      { kind: 'same', text: 'a' },
      { kind: 'del', text: 'b' },
      { kind: 'add', text: 'c' },
    ])
    expect(excerptDiff(undefined, undefined)).toEqual([])
  })
})

describe('RuleWatcherPage', () => {
  it('lists the pending proposals and the watched sources', async () => {
    renderWithProviders(<RuleWatcherPage />, MANAGER)

    expect(await screen.findByRole('link', { name: '#14 Duo koşulu netleşti' })).toHaveAttribute('href', '/admin/rules/proposals/14')
    expect(await screen.findByText('App Store Connect sürüm notları')).toBeInTheDocument()
    expect(screen.getByText('52c0…d118 → a91f…3b7e')).toBeInTheDocument()
    expect(screen.getByText('HTTP 503')).toBeInTheDocument()
    expect(lastRequestTo('/rules/proposals')?.body).toMatchObject({
      Filtering: { Criterias: [{ FilterBy: 'Status', Value: RuleProposalStatus.Pending }] },
    })
  })

  it('scans the sources now and shows every proposal', async () => {
    const { user } = renderWithProviders(<RuleWatcherPage />, MANAGER)

    await user.click(await screen.findByRole('button', { name: 'Şimdi tara' }))
    expect(await screen.findByText('6 kaynak okundu · 1 değişti · 0 okunamadı · 1 öneri açıldı.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Tümü' }))

    await expect.poll(() => (lastRequestTo('/rules/proposals')?.body as { Filtering?: unknown }).Filtering).toBeUndefined()
  })

  it('is closed to accounts that are not platform administrators', async () => {
    renderWithProviders(<RuleWatcherPage />)

    expect(await screen.findByText('Yetkin yok')).toBeInTheDocument()
  })

  it('shows the forbidden state when the permission is missing', async () => {
    server.use(endpoint('patch', '/rules/proposals', () => errorResponse('Forbidden', 'Yetkiniz yok.', 403)))

    renderWithProviders(<RuleWatcherPage />, MANAGER)

    expect(await screen.findByRole('heading', { name: 'Yetkin yok' })).toBeInTheDocument()
  })
})

describe('RuleProposalPage', () => {
  it('shows the source diff, the catalog patch and the impact', async () => {
    renderProposal()

    expect(await screen.findByRole('heading', { name: 'Duo koşulu netleşti' })).toBeInTheDocument()
    expect(screen.getByText('Claude (MCP) hazırladı')).toBeInTheDocument()
    expect(screen.getByText(/apps built with the iOS 27\.1 SDK/)).toBeInTheDocument()
    expect(screen.getByText('{ "id": "apple.screenshots.duo" }')).toBeInTheDocument()
    expect(screen.getByText('2 organizasyonda 2 uygulama.')).toBeInTheDocument()
    expect(screen.getByText('Koşulu değiştirmedim.')).toBeInTheDocument()
  })

  it('approves with notifications and a note', async () => {
    const { user } = renderProposal()

    await user.click(await screen.findByRole('button', { name: 'Onayla' }))
    const dialog = await screen.findByRole('dialog', { name: 'Öneri #14 onaylansın mı?' })
    await user.type(within(dialog).getByLabelText(/Not/), 'Tamam.')
    await user.click(within(dialog).getByRole('button', { name: 'Onayla ve yayınla' }))

    expect(await screen.findByText('Katalog 1.4 yayınlandı · 2 organizasyonda 5 kişiye bildirim gitti.')).toBeInTheDocument()
    expect(lastRequestTo('/rules/proposal/approve')?.body).toEqual({ ProposalId: 14, Notify: true, Note: 'Tamam.' })
  })

  it('shows the conflict when another administrator decided first', async () => {
    server.use(endpoint('put', '/rules/proposal/approve', () => errorResponse('RuleProposalDecided', 'Bu öneri zaten karara bağlandı.', 409)))
    const { user } = renderProposal()

    await user.click(await screen.findByRole('button', { name: 'Onayla' }))
    const dialog = await screen.findByRole('dialog', { name: 'Öneri #14 onaylansın mı?' })
    await user.click(within(dialog).getByRole('checkbox'))
    await user.click(within(dialog).getByRole('button', { name: 'Onayla ve yayınla' }))

    expect(await screen.findByText('Bu öneri zaten karara bağlandı.')).toBeInTheDocument()
    expect(lastRequestTo('/rules/proposal/approve')?.body).toMatchObject({ Notify: false })
  })

  it('rejects only with a reason', async () => {
    const { user } = renderProposal()

    await user.click(await screen.findByRole('button', { name: 'Reddet' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Öneri #14 reddedilsin mi?' })
    expect(within(dialog).getByRole('button', { name: 'Reddet' })).toBeDisabled()
    await user.click(within(dialog).getByRole('radio', { name: 'Kaynak henüz kararsız' }))
    await user.click(within(dialog).getByRole('button', { name: 'Reddet' }))

    expect(await screen.findByText('Öneri reddedildi.')).toBeInTheDocument()
    expect(lastRequestTo('/rules/proposal/reject')?.body).toEqual({ ProposalId: 14, Reason: RuleProposalRejectReason.SourceUnstable, Note: null })
  })

  it('prepares the rule ids and the patch', async () => {
    const { user } = renderProposal()

    await user.click(await screen.findByRole('button', { name: 'Hazırla' }))
    const dialog = await screen.findByRole('dialog', { name: 'Katalog yamasını hazırla' })
    await user.clear(within(dialog).getByLabelText('Kural kimlikleri'))
    await user.type(within(dialog).getByLabelText('Kural kimlikleri'), 'apple.a, apple.b')
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }))

    expect(await screen.findByText('Öneri hazırlandı.')).toBeInTheDocument()
    expect(lastRequestTo('/rules/proposal/prepare')?.body).toMatchObject({ ProposalId: 14, RuleIds: ['apple.a', 'apple.b'] })
  })

  it('shows a decided proposal without actions', async () => {
    phase4State.proposal = ruleProposal({
      Status: RuleProposalStatus.Rejected,
      RejectReason: RuleProposalRejectReason.Misread,
      DecidedByUserName: 'deniz@kuzeylab.dev',
      DecidedDate: '2026-11-18T12:00:00Z',
      Impact: [],
      CatalogPatch: undefined,
      SourceExcerptBefore: undefined,
      SourceExcerptAfter: undefined,
    })

    renderProposal()

    expect(await screen.findByText(/Kaynak yanlış yorumlanmış/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Onayla' })).not.toBeInTheDocument()
    expect(screen.getByText('Etkilenen uygulama yok.')).toBeInTheDocument()
    expect(screen.getByText(/Henüz katalog yaması yok/)).toBeInTheDocument()
  })
})
