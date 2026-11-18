import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { Modal } from './Modal'
import { Button, Field, Input } from './primitives'

export interface ConfirmOptions {
  title: string
  message?: ReactNode
  confirmLabel?: string
  cancelLabel?: string
  // A destructive action: the confirm button is red and says what it does (design 18.18).
  danger?: boolean
  // Irreversible or store-bound actions: the user types this text exactly before confirming (design 19: typed confirm).
  typeToConfirm?: string
}

type Confirm = (options: ConfirmOptions) => Promise<boolean>

const ConfirmContext = createContext<Confirm | null>(null)

interface Pending extends ConfirmOptions {
  resolve: (ok: boolean) => void
}

// The app's only confirmation dialog: window.confirm/alert/prompt are not used anywhere.
export function ConfirmProvider({ children }: { children: ReactNode }) {
  const t = useT()
  const [pending, setPending] = useState<Pending | null>(null)
  const [typed, setTyped] = useState('')
  const cancelRef = useRef<HTMLButtonElement>(null)

  const confirm = useCallback<Confirm>(
    (options) =>
      new Promise<boolean>((resolve) => {
        setTyped('')
        setPending({ ...options, resolve })
      }),
    [],
  )

  const finish = (ok: boolean) => {
    pending?.resolve(ok)
    setPending(null)
  }

  // Case sensitive, outer spaces trimmed.
  const matches = !pending?.typeToConfirm || typed.trim() === pending.typeToConfirm
  const value = useMemo(() => confirm, [confirm])

  return (
    <ConfirmContext.Provider value={value}>
      {children}
      <Modal
        open={!!pending}
        onClose={() => finish(false)}
        title={pending?.title}
        size="sm"
        role="alertdialog"
        initialFocus={cancelRef}
        footer={
          <>
            <Button ref={cancelRef} variant="ghost" onClick={() => finish(false)}>
              {pending?.cancelLabel ?? t.common.cancel}
            </Button>
            <Button variant={pending?.danger ? 'danger' : 'primary'} disabled={!matches} onClick={() => finish(true)}>
              {pending?.confirmLabel ?? t.common.confirm}
            </Button>
          </>
        }
      >
        {pending?.message ? <div className="text-[13.5px] text-fg2">{pending.message}</div> : null}
        {pending?.typeToConfirm ? (
          <Field
            label={interpolate(t.common.typeToConfirm, { text: pending.typeToConfirm })}
            error={typed.trim() && !matches ? t.common.typeToConfirmMismatch : null}
            hint={matches && typed ? t.common.typeToConfirmMatch : undefined}
          >
            <Input value={typed} onChange={(event) => setTyped(event.target.value)} autoComplete="off" spellCheck={false} />
          </Field>
        ) : null}
      </Modal>
    </ConfirmContext.Provider>
  )
}

export function useConfirm(): Confirm {
  const ctx = useContext(ConfirmContext)
  if (!ctx) {
    throw new Error('useConfirm must be used within ConfirmProvider')
  }
  return ctx
}
