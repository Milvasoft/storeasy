import { useState, type ReactNode } from 'react'
import { Copy } from 'lucide-react'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Checkbox } from '@/components/ui/primitives'
import { useT } from '@/lib/i18n/I18nContext'
import { showToast } from '@/lib/toast/toast'

async function copyText(value: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(value)
    return true
  } catch {
    return false
  }
}

/** A small "Copy" button for a snippet or an address; the text goes to the clipboard and a toast says so. */
export function CopyButton({ value, label, children }: { value: string; label: string; children?: ReactNode }) {
  const base = useT()
  return (
    <Button
      size="sm"
      aria-label={label}
      onClick={async () => {
        if (await copyText(value)) {
          showToast(base.common.copied, 'success')
        } else {
          showToast(base.common.copyFailed, 'error')
        }
      }}
    >
      <Copy aria-hidden />
      {children ?? base.common.copy}
    </Button>
  )
}

/** A titled code block with its copy button (setup snippets). */
export function Snippet({ title, code, hint, copyLabel }: { title: string; code: string; hint?: ReactNode; copyLabel: string }) {
  return (
    <div className="grid gap-1.5">
      <span className="flex items-center justify-between gap-2">
        <b className="text-[13px]">{title}</b>
        <CopyButton value={code} label={copyLabel} />
      </span>
      <pre className="code whitespace-pre-wrap break-all" aria-label={title}>
        {code}
      </pre>
      {hint ? <span className="text-xs text-fg3">{hint}</span> : null}
    </div>
  )
}

/**
 * A secret shown once (design 13.10, 16.13): it lives only in this dialog. Outside clicks and Escape do not close it,
 * and "Done" stays off until the user says the value is saved; closing drops the value from memory.
 */
export function ShownOnceDialog({
  title,
  description,
  warning,
  ackLabel,
  onClose,
  children,
}: {
  title: string
  description?: ReactNode
  warning: ReactNode
  ackLabel: string
  onClose: () => void
  children: ReactNode
}) {
  const base = useT()
  const [saved, setSaved] = useState(false)
  return (
    <Modal
      open
      dismissible={false}
      onClose={onClose}
      title={title}
      description={description}
      footer={
        <Button variant="primary" disabled={!saved} onClick={onClose}>
          {base.common.done}
        </Button>
      }
    >
      <div className="grid gap-3">
        {children}
        <Callout tone="warning">{warning}</Callout>
        <Checkbox checked={saved} onChange={setSaved}>
          {ackLabel}
        </Checkbox>
      </div>
    </Modal>
  )
}
