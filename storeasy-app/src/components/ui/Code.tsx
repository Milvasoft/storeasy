import { useState, type ReactNode } from 'react'
import { Check, Copy } from 'lucide-react'
import { useT } from '@/lib/i18n/I18nContext'
import { showToast } from '@/lib/toast/toast'
import { Button } from './primitives'

export interface DiffLine {
  kind: 'add' | 'del' | 'same'
  text: ReactNode
}

// Every store write is shown as a diff first (design 19: Diff). The line mark is text (+ / -), not only color.
export function DiffView({ title, lines }: { title: ReactNode; lines: DiffLine[] }) {
  return (
    <div className="diff">
      <div className="diff-h">{title}</div>
      {lines.map((line, index) => (
        <div key={index} className={line.kind === 'same' ? 'dl' : `dl ${line.kind}`}>
          <span>{line.text}</span>
        </div>
      ))}
    </div>
  )
}

export function CodeBlock({ children, label }: { children: string; label: string }) {
  return (
    <pre className="code" aria-label={label}>
      {children}
    </pre>
  )
}

async function copyText(value: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(value)
    return true
  } catch {
    return false
  }
}

// A value shown once (an invitation link, a token): monospace, dashed, with a copy button.
export function SecretBox({ value, onCopied }: { value: string; onCopied?: () => void }) {
  const t = useT()
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    if (await copyText(value)) {
      setCopied(true)
      onCopied?.()
      showToast(t.common.copied, 'success')
    } else {
      showToast(t.common.copyFailed, 'error')
    }
  }
  return (
    <div className="secret">
      <span className="min-w-0 flex-1 break-all">{value}</span>
      <Button size="sm" onClick={() => void copy()}>
        {copied ? <Check aria-hidden /> : <Copy aria-hidden />}
        {copied ? t.common.copied : t.common.copy}
      </Button>
    </div>
  )
}
