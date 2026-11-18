import { useSyncExternalStore } from 'react'
import { createPortal } from 'react-dom'
import { CircleAlert, CircleCheck, Info, TriangleAlert, X } from 'lucide-react'
import { useT } from '@/lib/i18n/I18nContext'
import { dismissToast, getToasts, subscribeToasts, type ToastType } from '@/lib/toast/toast'
import { IconButton } from './primitives'

const TONE: Record<ToastType, { className: string; icon: JSX.Element }> = {
  success: { className: 'ok', icon: <CircleCheck aria-hidden /> },
  error: { className: 'dng', icon: <CircleAlert aria-hidden /> },
  info: { className: 'info', icon: <Info aria-hidden /> },
  warning: { className: 'warn', icon: <TriangleAlert aria-hidden /> },
}

// Bottom right (full width above the bottom edge on phones); the tone is in the icon, the text always says it.
export function Toaster() {
  const t = useT()
  const items = useSyncExternalStore(subscribeToasts, getToasts)
  return createPortal(
    <div className="toasts" role="status" aria-live="polite">
      {items.map((item) => (
        <div key={item.id} className={`toast ${TONE[item.type].className} animate-toast-in`}>
          {TONE[item.type].icon}
          <div className="min-w-0 flex-1">
            <b className="block font-semibold">{item.message}</b>
            {item.detail ? <span className="text-fg2">{item.detail}</span> : null}
          </div>
          <IconButton label={t.common.close} size="sm" ghost onClick={() => dismissToast(item.id)}>
            <X aria-hidden />
          </IconButton>
        </div>
      ))}
    </div>,
    document.body,
  )
}
