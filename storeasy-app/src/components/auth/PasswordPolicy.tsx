import { Check, Circle } from 'lucide-react'
import { PASSWORD_POLICY } from '@/lib/api/textLimits'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { passwordRules, type PasswordRule } from '@/lib/validation'

const RULES: PasswordRule[] = ['length', 'case', 'digit', 'symbol']

/** The live password rules under the field (design 5.10): a met rule gets a green tick and says so to screen readers. */
export function PasswordPolicy({ value, id }: { value: string; id?: string }) {
  const t = useT()
  const met = passwordRules(value)
  const label = (rule: PasswordRule) =>
    rule === 'length' ? interpolate(t.password.rules.length, { count: PASSWORD_POLICY.minLength }) : t.password.rules[rule]
  return (
    <ul className="pol" id={id} aria-label={t.password.rulesLabel}>
      {RULES.map((rule) => (
        <li key={rule} className={met[rule] ? 'ok' : undefined}>
          {met[rule] ? <Check aria-hidden /> : <Circle aria-hidden />}
          <span>{label(rule)}</span>
          <span className="sr-only">{met[rule] ? t.password.met : t.password.notMet}</span>
        </li>
      ))}
    </ul>
  )
}
