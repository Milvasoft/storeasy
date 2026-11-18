import { useState } from 'react'
import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DatePicker, presetRange } from '@/components/ui/DatePicker'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button } from '@/components/ui/primitives'
import { renderWithProviders } from '../../support/render'

function ModalHarness() {
  const [open, setOpen] = useState(false)
  return (
    <>
      <Button onClick={() => setOpen(true)}>Aç</Button>
      <Modal open={open} onClose={() => setOpen(false)} title="Başlık" footer={<Button>Son</Button>}>
        <input aria-label="İlk alan" />
      </Modal>
    </>
  )
}

function SelectHarness() {
  const [value, setValue] = useState<string | null>(null)
  return (
    <>
      <Select
        aria-label="Ülke"
        searchable
        value={value}
        onChange={setValue}
        options={[
          { value: 'se', label: 'İsveç' },
          { value: 'ip', label: 'Isparta' },
          { value: 'de', label: 'Almanya' },
          { value: 'x', label: 'Kapalı', disabled: true },
        ]}
      />
      <output aria-label="Seçilen">{value}</output>
    </>
  )
}

function DateHarness() {
  const [value, setValue] = useState<string | null>(null)
  return (
    <>
      <DatePicker aria-label="Son tarih" value={value} onChange={setValue} />
      <output aria-label="Değer">{value}</output>
    </>
  )
}

describe('Modal', () => {
  it('is a labelled dialog that keeps Tab inside, closes on Escape and gives focus back', async () => {
    const { user } = renderWithProviders(<ModalHarness />, { authed: false })

    await user.click(screen.getByRole('button', { name: 'Aç' }))
    const dialog = screen.getByRole('dialog', { name: 'Başlık' })
    expect(within(dialog).getByLabelText('İlk alan')).toHaveFocus()
    await user.tab()
    expect(within(dialog).getByRole('button', { name: 'Son' })).toHaveFocus()
    await user.tab()
    expect(within(dialog).getByRole('button', { name: 'Kapat' })).toHaveFocus()
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Aç' })).toHaveFocus()
  })
})

describe('Select', () => {
  it('searches with Turkish folding and picks with the keyboard', async () => {
    const { user } = renderWithProviders(<SelectHarness />, { authed: false })

    await user.click(screen.getByRole('button', { name: 'Ülke' }))
    await user.keyboard('is')
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['İsveç', 'Isparta'])
    await user.keyboard('{ArrowDown}{Enter}')

    expect(screen.getByLabelText('Seçilen')).toHaveTextContent('ip')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('says there is no result for an unknown term', async () => {
    const { user } = renderWithProviders(<SelectHarness />, { authed: false })

    await user.click(screen.getByRole('button', { name: 'Ülke' }))
    await user.keyboard('zz')

    expect(screen.getByText('"zz" için sonuç yok')).toBeInTheDocument()
  })
})

describe('DatePicker', () => {
  it('picks a day as yyyy-MM-dd and clears it', async () => {
    const { user } = renderWithProviders(<DateHarness />, { authed: false })

    await user.click(screen.getByRole('button', { name: 'Son tarih' }))
    const grid = screen.getByRole('grid')
    await user.click(
      within(grid)
        .getAllByRole('gridcell')
        .find((cell) => cell.textContent === '15' && !cell.className.includes('text-fg3'))!,
    )

    expect(screen.getByLabelText('Değer').textContent).toMatch(/^\d{4}-\d{2}-15$/)
    await user.click(screen.getByRole('button', { name: 'Temizle' }))
    expect(screen.getByLabelText('Değer')).toHaveTextContent('')
  })

  it('computes the ready ranges from a given day', () => {
    expect(presetRange('last7', '2026-11-18')).toEqual({ from: '2026-11-12', to: '2026-11-18' })
    expect(presetRange('lastMonth', '2026-11-18')).toEqual({ from: '2026-10-01', to: '2026-10-31' })
  })
})
