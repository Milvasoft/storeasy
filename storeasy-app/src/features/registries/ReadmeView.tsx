import { useState } from 'react'
import ReactMarkdown, { type Components } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Chip } from '@/components/ui/primitives'
import type { RegistriesDictionary } from './i18n/en'
import { ReadmeFormat } from './types'

// A package's README: a rendered preview for Markdown (GitHub flavoured: tables, task lists) next to its source. Raw
// HTML in the Markdown is never rendered, links open in a new tab and only https images load. HTML and plain text
// READMEs stay as text.

const COMPONENTS: Components = {
  a: ({ href, children }) => (
    <a className="lnk" href={href} target="_blank" rel="noopener noreferrer nofollow">
      {children}
    </a>
  ),
  img: ({ src, alt }) =>
    typeof src === 'string' && src.startsWith('https://') ? (
      <img src={src} alt={alt ?? ''} loading="lazy" referrerPolicy="no-referrer" className="inline-block max-w-full" />
    ) : (
      <span className="text-fg3">{alt}</span>
    ),
}

export function ReadmeView({ readme, format, t }: { readme: string; format: ReadmeFormat | null; t: RegistriesDictionary }) {
  const markdown = format === ReadmeFormat.Markdown
  const [view, setView] = useState<'preview' | 'source'>(markdown ? 'preview' : 'source')

  return (
    <div className="grid gap-2">
      {markdown ? (
        <div className="flex gap-2" role="group" aria-label={t.listing.readme}>
          <Chip selected={view === 'preview'} onClick={() => setView('preview')}>
            {t.listing.readmePreview}
          </Chip>
          <Chip selected={view === 'source'} onClick={() => setView('source')}>
            {t.listing.readmeSource}
          </Chip>
        </div>
      ) : null}
      {format === ReadmeFormat.Html ? <p className="text-fg3">{t.listing.readmeHtml}</p> : null}
      {markdown && view === 'preview' ? (
        <div className="md max-h-[640px] overflow-auto">
          <ReactMarkdown remarkPlugins={[remarkGfm]} components={COMPONENTS}>
            {readme}
          </ReactMarkdown>
        </div>
      ) : (
        <pre className="code max-h-[480px] overflow-auto whitespace-pre-wrap">{readme}</pre>
      )}
    </div>
  )
}
