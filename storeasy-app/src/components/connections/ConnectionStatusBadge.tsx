import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { ConnectionStatus } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'

const TONE: Record<ConnectionStatus, BadgeTone> = {
  [ConnectionStatus.NotVerified]: 'neutral',
  [ConnectionStatus.Verified]: 'success',
  [ConnectionStatus.Rejected]: 'danger',
  [ConnectionStatus.Unreachable]: 'warning',
}

/** Not verified / Verified / Rejected / Unreachable (design 14.1), always as text with the dot. */
export function ConnectionStatusBadge({ status, verifying }: { status: ConnectionStatus; verifying?: boolean }) {
  const t = useT()
  if (verifying) {
    return (
      <Badge tone="info" live>
        {t.connections.verifying}
      </Badge>
    )
  }
  const label =
    status === ConnectionStatus.Verified
      ? t.enums.connectionStatus.verified
      : status === ConnectionStatus.Rejected
        ? t.enums.connectionStatus.rejected
        : status === ConnectionStatus.Unreachable
          ? t.enums.connectionStatus.unreachable
          : t.enums.connectionStatus.notVerified
  return (
    <Badge tone={TONE[status]} dot>
      {label}
    </Badge>
  )
}
