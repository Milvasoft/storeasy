import { fireEvent, screen, waitFor } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { StudioRoutes } from '@/features/studio/StudioRoutes'
import type { MockupSpec } from '@/features/studio/types'
import { renderWithProviders } from '../../support/render'
import { lastRequestTo } from '../../support/state'
import { setupStudioApi } from '../../support/studio'

setupStudioApi()

async function open() {
  const view = renderWithProviders(<StudioRoutes />, { route: '/studio/12', path: '/studio/*' })
  await screen.findByRole('heading', { name: 'Mağaza seti 2026', level: 1 })
  return view
}

function type(label: string, value: string) {
  const input = screen.getByLabelText(label)
  fireEvent.focus(input)
  fireEvent.change(input, { target: { value } })
  fireEvent.blur(input)
}

async function choose(user: UserEvent, label: string, option: string) {
  await user.click(screen.getByLabelText(label))
  await user.click(await screen.findByRole('option', { name: option }))
}

async function savedSpec(user: UserEvent): Promise<MockupSpec> {
  await user.click(screen.getByRole('button', { name: 'Kaydet' }))
  await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
  return (lastRequestTo('/Mockups/template')?.body as { Spec: MockupSpec }).Spec
}

describe('template editor: properties', () => {
  it('sets a gradient background with two colors and an angle', async () => {
    const { user } = await open()

    await user.click(screen.getByRole('radio', { name: 'Gradyan' }))
    type('İkinci renk', '#0F4C45')
    type('Açı (°)', '160')
    fireEvent.focus(screen.getByLabelText('Renk'))
    fireEvent.change(screen.getByLabelText('Renk'), { target: { value: 'zz' } })

    expect(screen.getByText('Rengi #RRGGBB olarak yaz.')).toBeInTheDocument()
    const spec = await savedSpec(user)
    expect(spec.Background).toMatchObject({ Kind: 'gradient', Colors: ['#4636FF', '#0F4C45'], Angle: 160 })
  })

  it('offers the background image upload for an image background', async () => {
    const { user } = await open()

    await user.click(screen.getByRole('radio', { name: 'Görsel' }))

    expect(screen.getByText('Henüz arka plan görseli yok.')).toBeInTheDocument()
    expect(screen.getByText(/PNG ya da JPEG, en çok 20 MB/)).toBeInTheDocument()
  })

  it('sets the device frame, cutout, color, position, rotation and shadow', async () => {
    const { user } = await open()
    await user.click(screen.getByRole('button', { name: 'Cihaz' }))

    await choose(user, 'Çerçeve', 'Tablet')
    await choose(user, 'Kesik', 'Kamera deliği')
    type('Çerçeve rengi', '#222222')
    type('Dönüş (°)', '-5')
    type('Yatay (%)', '4')
    type('Dikey (%)', '30')
    type('Köşe (%)', '7')
    type('Kalınlık (%)', '0')
    await user.click(screen.getByRole('switch', { name: 'Gölge' }))

    const spec = await savedSpec(user)
    expect(spec.Frame).toMatchObject({
      Kind: 'tablet',
      Cutout: 'hole',
      Color: '#222222',
      Rotation: -5,
      OffsetX: 0.04,
      OffsetY: 0.3,
      CornerRadius: 0.07,
      Bezel: null,
      Shadow: false,
    })
  })

  it('sets the text font, position, alignment and the title style', async () => {
    const { user } = await open()
    await user.click(screen.getByRole('button', { name: 'Metin' }))

    await choose(user, 'Yazı tipi', 'Noto Sans')
    await user.click(screen.getByRole('radio', { name: 'Altta' }))
    await user.click(screen.getByRole('radio', { name: 'Baş' }))
    await user.click(screen.getByRole('switch', { name: 'Büyük harf' }))
    type('Kenar payı (%)', '6')
    type('Aralık (%)', '2')
    type('Başlık boyutu (%)', '9')
    await choose(user, 'Başlık kalınlığı', '800')
    type('Başlık en çok satır', '3')
    type('Başlık satır yüksekliği', '1.2')
    type('Başlık en küçük boyut (%)', '5')
    await user.click(screen.getByRole('switch', { name: 'Başlık sığdırmak için küçülür' }))
    type('Alt başlık rengi', '#EEEEEE')

    const spec = await savedSpec(user)
    expect(spec.Text).toMatchObject({ Font: 'noto-sans', Position: 'bottom', Alignment: 'start', Uppercase: true, Margin: 0.06, Gap: 0.02 })
    expect(spec.Text?.Title).toMatchObject({ Size: 0.09, Weight: 800, MaxLines: 3, LineHeight: 1.2, MinSize: 0.05, AutoFit: false })
    expect(spec.Text?.Subtitle?.Color).toBe('#EEEEEE')
  })

  it('adds a shape layer, edits it and removes it again', async () => {
    const { user } = await open()

    await user.click(screen.getByRole('button', { name: 'Şekil ekle' }))
    await choose(user, 'Şekil türü', 'Elips')
    type('Genişlik (%)', '50')
    type('Yükseklik (%)', '20')
    type('X (%)', '10')
    type('Y (%)', '40')
    type('Opaklık (%)', '50')
    type('Dolgu', '#FF0000')
    await user.click(screen.getByRole('switch', { name: 'Cihazın üstünde' }))
    const spec = await savedSpec(user)
    expect(spec.Layers).toEqual([
      expect.objectContaining({
        Type: 'shape',
        Shape: 'ellipse',
        Width: 0.5,
        Height: 0.2,
        X: 0.1,
        Y: 0.4,
        Opacity: 0.5,
        Color: '#FF0000',
        Above: true,
      }),
    ])

    await user.click(screen.getByRole('button', { name: 'Kaldır' }))

    expect(screen.queryByRole('button', { name: 'Şekil 1' })).not.toBeInTheDocument()
  })

  it('removes a per-language badge text and keeps the one for every language', async () => {
    const { user } = await open()

    await user.click(screen.getByRole('button', { name: 'Rozet ekle' }))
    type('Rozet metni, en-US', 'NEW!')
    type('Rozet metni, en-US', '')
    type('Metin boyutu (%)', '5')
    type('Metin rengi', '#000000')

    const spec = await savedSpec(user)
    expect(spec.Layers?.[0]).toMatchObject({ Texts: { '*': 'NEW' }, TextSize: 0.05, TextColor: '#000000' })
  })

  it('removes the screenshot of a slot for every language', async () => {
    const { user } = await open()

    await user.click(screen.getByRole('button', { name: 'Telefon kaldır' }))
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
    expect(lastRequestTo('/Mockups/template')?.body).toMatchObject({ Slots: [] })
  })

  it('changes the template languages and design class', async () => {
    const { user } = await open()
    await user.click(screen.getByRole('button', { name: 'Şablon' }))

    type('Diller', 'en-US, de-DE')
    await choose(user, 'Tasarım sınıfı', 'Telefon · 1080x1920')
    await user.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(lastRequestTo('/Mockups/template')?.method).toBe('PUT'))
    expect(lastRequestTo('/Mockups/template')?.body).toMatchObject({
      Locales: ['en-US', 'de-DE'],
      PrimaryLocale: 'en-US',
      Spec: { Canvas: { ClassKey: 'google.screenshots.phone' } },
    })
  })
})
