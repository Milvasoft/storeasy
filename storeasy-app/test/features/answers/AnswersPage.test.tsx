import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrgRole } from '@/lib/api/types'
import { SavedAnswerKind } from '@/features/common/types'
import { AnswersPage } from '@/features/answers/AnswersPage'
import { setupContentApi } from '../../support/content'
import { app, organization } from '../../support/factories'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo, requestsTo, testState } from '../../support/state'

setupContentApi()

// The save, not the read that follows it.
function lastSave() {
  return requestsTo('/Apps/answers')
    .filter((request) => request.method === 'PUT')
    .at(-1)
}

function render() {
  return renderWithProviders(<AnswersPage />, { route: '/apps/101/answers', path: '/apps/:appId/answers' })
}

describe('AnswersPage', () => {
  it('keeps the demo password write only: never shown, sent only when typed', async () => {
    const { user } = render()
    await user.click(await screen.findByRole('tab', { name: 'İnceleme giriş bilgileri' }))
    const password = await screen.findByLabelText('Demo hesap şifresi')

    expect(password).toHaveValue('')
    expect(screen.getByText(/Kayıtlı, şifreli\. Boş bırakırsan değişmez\./)).toBeInTheDocument()
    await user.type(password, 'Yeni!Sifre1')
    await user.click(screen.getByRole('button', { name: 'Cevapları kaydet' }))

    await screen.findByText('Kaydedildi.')
    expect(lastSave()?.body).toMatchObject({
      AppId: 101,
      Key: SavedAnswerKind.ReviewLogin,
      DemoPassword: 'Yeni!Sifre1',
      RemoveDemoAccountLogin: false,
      Answers: { ContactFirstName: 'Deniz', DemoAccountRequired: true, DemoAccountName: 'demo@kuzeylab.dev' },
    })
    expect(password).toHaveValue('')
  })

  it('reads the Play CSV, previews the CSV that goes to Play and plans sending it', async () => {
    const { user } = render()
    await user.click(await screen.findByRole('tab', { name: 'Data safety' }))

    await user.click(screen.getByLabelText(/Play Console'dan dışa aktarılan CSV/))
    await user.paste(
      'Question ID (machine readable),Response ID (machine readable),Response value,Answer requirement,Human-friendly question label\nPSL_DATA_COLLECTION,,true,,Collects data',
    )
    await user.click(screen.getByRole('button', { name: "CSV'yi oku" }))

    expect(screen.getByLabelText('Soru ID')).toHaveValue('PSL_DATA_COLLECTION')
    expect(screen.getByText(/PSL_DATA_COLLECTION,,true,,Collects data/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cevapları kaydet' }))
    await screen.findByText('Kaydedildi.')
    expect(lastSave()?.body).toMatchObject({
      Key: SavedAnswerKind.DataSafety,
      Answers: { Rows: [{ QuestionId: 'PSL_DATA_COLLECTION', ResponseId: null, ResponseValue: 'true', Label: 'Collects data' }] },
      DemoPassword: null,
    })
    await user.click(screen.getByRole('button', { name: "Play'e göndermeyi planla" }))
    await screen.findByRole('dialog', { name: 'de-DE anahtar kelimeler' })
    expect(lastRequestTo('/Apps/answers/data-safety')?.body).toMatchObject({ AppId: 101, DryRun: true })
  })

  it('copies chosen sections from another app and says which ones are overwritten', async () => {
    testState.apps = [app(), app({ Id: 102, Name: 'Defter' })]
    const { user } = render()
    await user.click(await screen.findByRole('button', { name: 'Başka uygulamadan kopyala' }))
    const dialog = await screen.findByRole('dialog', { name: 'Cevapları kopyala' })

    await user.click(await within(dialog).findByLabelText('Kaynak uygulama'))
    await user.click(await screen.findByRole('option', { name: 'Defter' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'İnceleme giriş bilgileri' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Hedef kitle' }))
    expect(within(dialog).getByText('Üzerine yazılır')).toBeInTheDocument()
    expect(within(dialog).getByText('Demo hesap şifresi hiçbir zaman kopyalanmaz.')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: '2 bölümü kopyala' }))

    await screen.findByText('Cevaplar kopyalandı.')
    expect(lastRequestTo('/Apps/answers/copy')?.body).toMatchObject({
      SourceAppId: 102,
      TargetAppId: 101,
      Keys: [SavedAnswerKind.ReviewLogin, SavedAnswerKind.TargetAudience],
    })
  })

  it('shows the answers read only to a Viewer', async () => {
    testState.organizations = [organization({ Role: OrgRole.Viewer })]
    const { user } = render()

    await user.click(await screen.findByRole('tab', { name: 'İnceleme notları' }))

    expect(screen.getByLabelText(/Notlar/)).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Cevapları kaydet' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Başka uygulamadan kopyala' })).not.toBeInTheDocument()
  })
})
