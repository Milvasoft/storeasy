import { useSyncExternalStore } from 'react'
import { Monitor, Moon, Sun } from 'lucide-react'
import { IconButton } from '@/components/ui/primitives'
import { Menu } from '@/components/ui/Menu'
import { SegmentedControl } from '@/components/ui/Tabs'
import { useT } from '@/lib/i18n/I18nContext'
import { getTheme, resolvedTheme, setTheme, subscribeTheme, THEMES, type Theme } from '@/lib/theme/theme'

export function useTheme(): Theme {
  return useSyncExternalStore(subscribeTheme, getTheme, () => 'system' as Theme)
}

const ICON = { light: Sun, dark: Moon, system: Monitor } as const

/** Light / dark / system as a segmented control (account menu, settings). */
export function ThemeSwitcher() {
  const t = useT()
  const theme = useTheme()
  return (
    <SegmentedControl
      label={t.theme.label}
      value={theme}
      onChange={setTheme}
      options={THEMES.map((value) => {
        const Icon = ICON[value]
        return {
          value,
          label: (
            <>
              <Icon className="h-3.5 w-3.5" aria-hidden />
              {t.theme[value]}
            </>
          ),
        }
      })}
    />
  )
}

/** The top bar's theme button: a small menu with the three choices. */
export function ThemeMenuButton() {
  const t = useT()
  const theme = useTheme()
  const Icon = resolvedTheme(theme) === 'dark' ? Moon : Sun
  return (
    <Menu
      label={t.theme.label}
      trigger={
        <IconButton label={t.theme.label} ghost>
          <Icon aria-hidden />
        </IconButton>
      }
      entries={[
        { key: 'heading', heading: t.theme.label },
        ...THEMES.map((value) => {
          const ItemIcon = ICON[value]
          return {
            key: value,
            label: t.theme[value],
            icon: <ItemIcon aria-hidden />,
            checked: theme === value,
            onSelect: () => setTheme(value),
          }
        }),
      ]}
    />
  )
}
