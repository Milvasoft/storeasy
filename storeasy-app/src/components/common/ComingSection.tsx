import type { ReactNode } from 'react'
import { Clock } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { EmptyState, PageHeader } from '@/components/ui/primitives'
import { useT } from '@/lib/i18n/I18nContext'

/**
 * A section whose API is not there yet (its feature flag is off): the page header and the section's designed first-use
 * state, marked "coming". No made-up data, no "under construction" placeholder (design 18.9).
 */
export function ComingSection({
  title,
  subtitle,
  emptyTitle,
  emptyBody,
  icon,
  action,
}: {
  title: string
  subtitle?: string
  emptyTitle: string
  emptyBody: string
  icon: ReactNode
  action?: ReactNode
}) {
  const t = useT()
  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        badge={
          <Badge tone="info" small>
            <Clock className="h-3 w-3" aria-hidden />
            {t.coming.badge}
          </Badge>
        }
      />
      <EmptyState icon={icon} title={emptyTitle} description={emptyBody} action={action}>
        <p className="!mt-2 text-xs text-fg3">{t.coming.note}</p>
      </EmptyState>
    </>
  )
}
