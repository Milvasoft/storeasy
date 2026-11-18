import { useNavigate } from 'react-router-dom'
import { Code, KeyRound, Languages, LogOut, Monitor, Moon, MoreHorizontal, Sun, UserRound } from 'lucide-react'
import { useTheme } from '@/components/common/ThemeSwitcher'
import { Menu } from '@/components/ui/Menu'
import { Avatar } from '@/components/ui/primitives'
import { SOURCE_URL } from '@/config'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useI18n } from '@/lib/i18n/I18nContext'
import { LANGS } from '@/lib/i18n/lang'
import { setTheme, THEMES } from '@/lib/theme/theme'

const THEME_ICON = { light: Sun, dark: Moon, system: Monitor } as const

/** The person row at the bottom of the menu; its menu opens upwards (design 16.1). Signing out asks nothing (16.2). */
export function UserMenu({ onNavigate }: { onNavigate?: () => void }) {
  const { t, lang, setLang } = useI18n()
  const { user, logout } = useAuth()
  const theme = useTheme()
  const navigate = useNavigate()
  if (!user) {
    return null
  }
  const name = displayName(user)
  const go = (path: string) => {
    navigate(path)
    onNavigate?.()
  }

  return (
    <Menu
      label={t.userMenu.label}
      placement="top-start"
      className="w-[260px]"
      trigger={
        <button type="button" className="me" aria-label={t.userMenu.label}>
          <Avatar initials={initialsOf(name)} />
          <span className="min-w-0 flex-1">
            <b className="truncate">{name}</b>
            <small className="truncate">{user.email}</small>
          </span>
          <MoreHorizontal className="h-4 w-4 flex-none text-fg3" aria-hidden />
        </button>
      }
      entries={[
        { key: 'account', icon: <UserRound aria-hidden />, label: t.userMenu.profile, onSelect: () => go('/account') },
        { key: 'security', icon: <KeyRound aria-hidden />, label: t.userMenu.security, onSelect: () => go('/account/security') },
        { key: 'theme-separator', separator: true },
        { key: 'theme-heading', heading: t.theme.label },
        ...THEMES.map((value) => {
          const Icon = THEME_ICON[value]
          return {
            key: `theme-${value}`,
            icon: <Icon aria-hidden />,
            label: t.theme[value],
            checked: theme === value,
            onSelect: () => setTheme(value),
          }
        }),
        { key: 'lang-heading', heading: t.language.label },
        ...LANGS.map((value) => ({
          key: `lang-${value}`,
          icon: <Languages aria-hidden />,
          label: t.language.names[value],
          checked: lang === value,
          onSelect: () => setLang(value),
        })),
        { key: 'source-separator', separator: true },
        {
          key: 'source',
          icon: <Code aria-hidden />,
          label: t.footer.sourceCode,
          onSelect: () => {
            window.open(SOURCE_URL, '_blank', 'noopener,noreferrer')
          },
        },
        { key: 'logout', icon: <LogOut aria-hidden />, label: t.userMenu.logout, danger: true, onSelect: () => void logout() },
      ]}
    />
  )
}
