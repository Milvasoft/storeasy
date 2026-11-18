import type { ReactNode } from 'react'
import { SearchX, ShieldAlert, TriangleAlert, WifiOff, Wrench } from 'lucide-react'
import { ApiError, FEATURE_DISABLED_KEY } from '@/lib/api/http'
import { useT } from '@/lib/i18n/I18nContext'
import { Button } from './primitives'

/** A full area state (design 18): icon tile, title, text, at most one way out. */
export function StateView({
  icon,
  title,
  description,
  action,
}: {
  icon: ReactNode
  title: string
  description?: ReactNode
  action?: ReactNode
}) {
  return (
    <div className="state" role="alert">
      <div className="sic">{icon}</div>
      <h2>{title}</h2>
      {description ? <p>{description}</p> : null}
      {action ? <div className="flex flex-wrap justify-center gap-2">{action}</div> : null}
    </div>
  )
}

/**
 * The error state of a screen (design 18.7, 18.8, 18.10, 18.11): forbidden, not found and switched-off features get their
 * own state and no retry; a network failure says the connection is gone; anything else offers "Try again".
 */
export function ScreenState({
  error,
  onRetry,
  forbiddenDetail,
  back,
}: {
  error: unknown
  onRetry?: () => void
  forbiddenDetail?: string
  back?: ReactNode
}) {
  const t = useT()
  const status = error instanceof ApiError ? error.statusCode : 0
  if (status === 403) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={t.states.forbiddenTitle}
        description={forbiddenDetail ?? t.states.forbiddenBody}
        action={back}
      />
    )
  }
  if (status === 404) {
    return (
      <StateView
        icon={<SearchX aria-hidden />}
        title={t.states.recordNotFoundTitle}
        description={t.states.recordNotFoundBody}
        action={back}
      />
    )
  }
  if (error instanceof ApiError && error.keys.includes(FEATURE_DISABLED_KEY)) {
    return (
      <StateView
        icon={<Wrench aria-hidden />}
        title={t.states.unavailableTitle}
        description={error.messages[0] ?? t.states.unavailableBody}
      />
    )
  }
  const retry = onRetry ? <RetryButton onRetry={onRetry} label={t.states.retry} /> : undefined
  if (!(error instanceof ApiError)) {
    return <StateView icon={<WifiOff aria-hidden />} title={t.states.offlineTitle} description={t.states.offlineBody} action={retry} />
  }
  return (
    <StateView
      icon={<TriangleAlert aria-hidden />}
      title={t.states.errorTitle}
      description={error.messages[0] ?? t.states.errorBody}
      action={retry}
    />
  )
}

function RetryButton({ onRetry, label }: { onRetry: () => void; label: string }) {
  return (
    <Button variant="inverse" onClick={onRetry}>
      {label}
    </Button>
  )
}
