import { forwardRef, type ReactNode } from 'react'
import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react'
import { cn } from '@/lib/utils/cn'

export type FeedbackTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

const TONE: Record<FeedbackTone, { className: string; icon: ReactNode }> = {
  neutral: { className: '', icon: <Info aria-hidden /> },
  info: { className: 'info', icon: <Info aria-hidden /> },
  success: { className: 'ok', icon: <CircleCheck aria-hidden /> },
  warning: { className: 'warn', icon: <TriangleAlert aria-hidden /> },
  danger: { className: 'dng', icon: <CircleAlert aria-hidden /> },
}

// An in-content note (design 19: callout). The tone lives in the icon and border; the text says it too.
export function Callout({
  tone = 'neutral',
  title,
  children,
  className,
}: {
  tone?: FeedbackTone
  title?: ReactNode
  children?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('callout', TONE[tone].className, className)}>
      {TONE[tone].icon}
      <div className="min-w-0">
        {title ? <b className="block">{title}</b> : null}
        {children ? <div className={cn(title ? 'mt-1 text-fg2' : undefined)}>{children}</div> : null}
      </div>
    </div>
  )
}

// A page wide strip above the content (offline, live stream reconnecting, read only view).
export function Banner({
  tone = 'neutral',
  children,
  action,
  role,
}: {
  tone?: FeedbackTone
  children: ReactNode
  action?: ReactNode
  role?: 'status' | 'alert'
}) {
  return (
    <div className={cn('banner', TONE[tone].className)} role={role}>
      {TONE[tone].icon}
      <div className="min-w-0 flex-1">{children}</div>
      {action}
    </div>
  )
}

// The error box above a form (design 5.2, 18.17): the server's messages, focusable so focus can move to it.
export const FormErrors = forwardRef<
  HTMLDivElement,
  { title: string; messages?: string[]; detail?: ReactNode; tone?: 'danger' | 'warning' | 'info' }
>(function FormErrors({ title, messages = [], detail, tone = 'danger' }, ref) {
  const icon = tone === 'danger' ? <CircleAlert className="h-[18px] w-[18px] flex-none text-danger" aria-hidden /> : TONE[tone].icon
  return (
    <div
      ref={ref}
      tabIndex={-1}
      role={tone === 'danger' ? 'alert' : 'status'}
      className={cn(
        'ferr outline-none',
        tone === 'warning' && '!border-warning/45 !bg-warning/[.07]',
        tone === 'info' && '!border-info/40 !bg-info/[.06]',
      )}
    >
      {tone === 'danger' ? (
        icon
      ) : (
        <span className={cn('flex-none [&>svg]:h-[18px] [&>svg]:w-[18px]', tone === 'warning' ? 'text-warningText' : 'text-info')}>
          {icon}
        </span>
      )}
      <div className="min-w-0">
        <b>{title}</b>
        {messages.length > 1 ? (
          <ul>
            {messages.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        ) : messages.length === 1 && messages[0] !== title ? (
          <div className="mt-1 text-fg2">{messages[0]}</div>
        ) : null}
        {detail ? <div className="mt-1 text-fg2">{detail}</div> : null}
      </div>
    </div>
  )
})
