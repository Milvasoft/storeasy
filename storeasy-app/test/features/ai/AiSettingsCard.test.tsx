import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { AiSettingsCard } from '@/features/ai/AiSettingsCard'
import { looksLikeAnthropicKey } from '@/features/ai/api'
import { organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

// Shaped like an Anthropic key, but not one.
const FAKE_KEY = 'sk-ant-test-0000000000000000000000AbCd'

describe('looksLikeAnthropicKey', () => {
  it('accepts only the shape of an Anthropic key', () => {
    expect(looksLikeAnthropicKey(`  ${FAKE_KEY} `)).toBe(true)
    expect(looksLikeAnthropicKey('sk-proj-0000000000')).toBe(false)
    expect(looksLikeAnthropicKey('sk-ant-')).toBe(false)
    expect(looksLikeAnthropicKey('sk-ant-test 0000')).toBe(false)
  })
})

describe('AiSettingsCard', () => {
  it('shows only the last four characters of the saved key', async () => {
    renderWithProviders(<AiSettingsCard />)

    expect(await screen.findByText('Sonu Xy9Z ile biten anahtar')).toBeInTheDocument()
    expect(screen.getByText(/Ayşe Yılmaz kaydetti/)).toBeInTheDocument()
    expect(screen.getByText('Model: claude-haiku-5-5')).toBeInTheDocument()
    expect(screen.queryByLabelText('Anthropic API anahtarı')).not.toBeInTheDocument()
  })

  it('saves a new key, checks its shape first and clears the field', async () => {
    testState.aiSettings = { HasApiKey: false, Model: 'claude-haiku-5-5' }
    const { user } = renderWithProviders(<AiSettingsCard />)

    const field = await screen.findByLabelText('Anthropic API anahtarı')
    expect(field).toHaveAttribute('type', 'password')
    await user.type(field, 'sk-proj-wrong')
    await user.click(screen.getByRole('button', { name: 'Anahtarı kaydet' }))
    expect(screen.getByText('Anthropic API anahtarı sk-ant- ile başlar ve boşluk içermez.')).toBeInTheDocument()
    expect(requestsTo('/Organizations/ai-settings/key')).toHaveLength(0)

    await user.clear(field)
    await user.type(field, FAKE_KEY)
    await user.click(screen.getByRole('button', { name: 'Anahtarı kaydet' }))

    expect(await screen.findByText('Anahtar kaydedildi ve Anthropic ile doğrulandı.')).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/ai-settings/key')?.body).toEqual({ OrganizationId: 7, ApiKey: FAKE_KEY })
    expect(await screen.findByText('Sonu AbCd ile biten anahtar')).toBeInTheDocument()
    expect(screen.queryByDisplayValue(FAKE_KEY)).not.toBeInTheDocument()
  })

  it('keeps a key Anthropic rejects out and says why', async () => {
    const { user } = renderWithProviders(<AiSettingsCard />)

    await user.click(await screen.findByRole('button', { name: 'Anahtarı değiştir' }))
    await user.type(screen.getByLabelText('Anthropic API anahtarı'), 'sk-ant-test-rejected-000000')
    await user.click(screen.getByRole('button', { name: 'Anahtarı kaydet' }))

    expect(await screen.findByText('Anthropic API anahtarını reddetti.')).toBeInTheDocument()
    expect(screen.getByLabelText('Anthropic API anahtarı')).toBeInTheDocument()
  })

  it('warns when the key could not be checked', async () => {
    const { user } = renderWithProviders(<AiSettingsCard />)

    await user.click(await screen.findByRole('button', { name: 'Anahtarı değiştir' }))
    await user.type(screen.getByLabelText('Anthropic API anahtarı'), 'sk-ant-test-offline-000000')
    await user.click(screen.getByRole('button', { name: 'Anahtarı kaydet' }))

    expect(await screen.findByText(/doğrulamak için Anthropic'e ulaşılamadı/)).toBeInTheDocument()
  })

  it('removes the key after a confirmation', async () => {
    const { user } = renderWithProviders(<AiSettingsCard />)

    await user.click(await screen.findByRole('button', { name: 'Anahtarı kaldır' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Anthropic API anahtarı kaldırılsın mı?' })
    await user.click(within(dialog).getByRole('button', { name: 'Anahtarı kaldır' }))

    expect(await screen.findByText('Anahtar kaldırıldı.')).toBeInTheDocument()
    expect(requestsTo('/Organizations/ai-settings/key').at(-1)?.method).toBe('DELETE')
    expect(await screen.findByText(/Henüz anahtar yok/)).toBeInTheDocument()
  })

  it('is read only for members who do not manage connections', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    renderWithProviders(<AiSettingsCard />)

    expect(await screen.findByText('Anahtarı yalnızca mağaza bağlantılarını yöneten üyeler değiştirebilir.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anahtarı değiştir' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anahtarı kaldır' })).not.toBeInTheDocument()
  })
})

describe('AiSettingsCard models', () => {
  it('offers the models of each feature with their prices and saves the choice', async () => {
    const { user } = renderWithProviders(<AiSettingsCard />)

    const preReview = await screen.findByRole('radiogroup', { name: 'Ön inceleme' })
    expect(within(preReview).getByRole('radio', { name: /Claude Sonnet 5.5/ })).toBeChecked()
    expect(within(preReview).getByText('Milyon token başına $2 / $10 (girdi / çıktı)')).toBeInTheDocument()
    const replies = screen.getByRole('radiogroup', { name: 'Yorum yanıtı taslakları' })
    expect(within(replies).getByRole('radio', { name: /Claude Haiku 5.5/ })).toBeChecked()
    expect(within(replies).getByText('Milyon token başına $0.10 / $0.50 (girdi / çıktı)')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Modelleri kaydet' })).toBeDisabled()

    await user.click(within(preReview).getByRole('radio', { name: /Claude Opus 5.5/ }))
    await user.click(screen.getByRole('button', { name: 'Modelleri kaydet' }))

    expect(await screen.findByText('Modeller kaydedildi.')).toBeInTheDocument()
    expect(lastRequestTo('/Organizations/ai-settings/models')?.body).toEqual({
      OrganizationId: 7,
      ReplyDraftModel: 'claude-haiku-5-5',
      PreReviewModel: 'claude-opus-5-5',
    })
  })

  it('shows the models read only to members who do not manage connections', async () => {
    testState.organizations = [organization({ Role: OrgRole.Editor })]

    renderWithProviders(<AiSettingsCard />)

    expect(await screen.findByText('Yanıt taslakları: Claude Haiku 5.5. Ön inceleme: Claude Sonnet 5.5.')).toBeInTheDocument()
    expect(screen.queryByRole('radiogroup', { name: 'Ön inceleme' })).not.toBeInTheDocument()
  })
})
