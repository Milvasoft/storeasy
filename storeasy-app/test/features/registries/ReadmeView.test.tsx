import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { registriesEn as en } from '@/features/registries/i18n/en'
import { ReadmeView } from '@/features/registries/ReadmeView'
import { ReadmeFormat } from '@/features/registries/types'

const README = '# Kabuk\n\nSee [docs](https://example.com/docs).\n\n<script>window.hacked = true</script>\n\n| a | b |\n|---|---|\n| 1 | 2 |\n'

describe('ReadmeView', () => {
  it('previews Markdown without raw HTML and switches to the source', async () => {
    render(<ReadmeView readme={README} format={ReadmeFormat.Markdown} t={en} />)

    expect(screen.getByRole('heading', { name: 'Kabuk' })).toBeInTheDocument()
    const link = screen.getByRole('link', { name: 'docs' })
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'))
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(document.querySelector('script')).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: en.listing.readmeSource }))
    expect(screen.getByText(/# Kabuk/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Kabuk' })).not.toBeInTheDocument()
  })

  it('shows HTML and text READMEs as text only', () => {
    render(<ReadmeView readme="<h1>Title</h1>" format={ReadmeFormat.Html} t={en} />)

    expect(screen.queryByRole('button', { name: en.listing.readmePreview })).not.toBeInTheDocument()
    expect(screen.getByText('<h1>Title</h1>')).toBeInTheDocument()
  })
})
