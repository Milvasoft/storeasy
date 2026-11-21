import { ArrowDownRight, ArrowUpRight } from 'lucide-react'
import { useI18n } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { cn } from '@/lib/utils/cn'
import { formatChange, formatPercent } from '@/features/store-metrics/labels'
import { useImpactText } from './i18n'

// A relative change with an arrow ("↗ +12%"), green up, red down; the text says it in words for screen readers. With
// lowerIsBetter (keyword ranks) the colours swap.

export function ChangeArrow({ change, lowerIsBetter }: { change: number | null | undefined; lowerIsBetter?: boolean }) {
  const t = useImpactText()
  const { lang } = useI18n()
  const text = formatChange(change, lang)
  if (text == null || change == null) {
    return <span className="text-xs text-fg3">{t.kpi.none}</span>
  }
  if (change === 0) {
    return <span className="text-xs text-fg3">{t.kpi.flat}</span>
  }
  const up = change > 0
  const good = lowerIsBetter ? !up : up
  const label = interpolate(up ? t.kpi.up : t.kpi.down, { value: formatPercent(Math.abs(change), lang) })
  return (
    <span className={cn('inline-flex items-center gap-1 text-xs tabular-nums', good ? 'text-success' : 'text-danger')} title={label}>
      {up ? <ArrowUpRight className="h-3.5 w-3.5" aria-hidden /> : <ArrowDownRight className="h-3.5 w-3.5" aria-hidden />}
      <span aria-hidden>{text}</span>
      <span className="sr-only">{label}</span>
    </span>
  )
}
