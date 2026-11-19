import { useId } from 'react'
import { storeCode } from '@/components/ui/Badge'
import { iconsForStore, StoreIcon, STORE_LOGO_ICON, type StoreIconKey } from '@/components/ui/StoreIcons'
import type { StoreKind } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { cn } from '@/lib/utils/cn'

/**
 * Picks the icon of a store connection: "none" (the store's text code) or one of the preset icons, the store's own logo
 * first. A radio group, so arrow keys move between the options.
 */
export function StoreIconPicker({
  store,
  value,
  onChange,
}: {
  store: StoreKind
  value: StoreIconKey | null
  onChange: (icon: StoreIconKey | null) => void
}) {
  const t = useT()
  const name = useId()
  const hintId = `${name}-hint`
  const options: Array<StoreIconKey | null> = [null, ...iconsForStore(store)]
  return (
    <fieldset className="m-0 grid gap-2 border-0 p-0" aria-describedby={hintId}>
      <legend className="lbl mb-2 p-0">
        {t.connections.icon}
        <span className="opt">{t.common.optional}</span>
      </legend>
      <div className="grid grid-cols-4 gap-2 sm:grid-cols-6">
        {options.map((icon) => {
          const selected = value === icon
          const label = icon ? t.connections.iconNames[icon] : t.connections.iconNone
          return (
            <label
              key={icon ?? 'none'}
              className={cn(
                'grid cursor-pointer justify-items-center gap-1.5 rounded-sm border px-1 py-2 text-center text-[11.5px] leading-tight text-fg2',
                'has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent',
                selected ? 'border-accent bg-accent/10 font-semibold text-fg' : 'border-border hover:bg-bg2',
              )}
            >
              <input
                type="radio"
                className="sr-only"
                name={name}
                checked={selected}
                onChange={() => onChange(icon)}
                aria-label={icon ? label : `${label} (${t.connections.iconNoneHint})`}
              />
              {icon ? (
                <StoreIcon icon={icon} className="lg" />
              ) : (
                <span className="st" aria-hidden>
                  <b style={{ height: 28, minWidth: 28 }}>{storeCode(store)}</b>
                </span>
              )}
              <span className="max-w-full truncate" aria-hidden>
                {label}
              </span>
            </label>
          )
        })}
      </div>
      <span id={hintId} className="hint">
        {t.connections.iconHint} {value === STORE_LOGO_ICON[store] ? t.connections.logoNote : null}
      </span>
    </fieldset>
  )
}
