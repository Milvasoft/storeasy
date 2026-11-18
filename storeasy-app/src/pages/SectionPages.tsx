import { Link } from 'react-router-dom'
import {
  BookOpen,
  CalendarClock,
  CloudUpload,
  Flag,
  History,
  Images,
  Languages,
  LayoutGrid,
  MessageSquare,
  Radar,
  Server,
  Shield,
  ShieldAlert,
  Terminal,
  User,
  Webhook,
  type LucideIcon,
} from 'lucide-react'
import { ComingSection } from '@/components/common/ComingSection'
import { StateView } from '@/components/ui/ScreenState'
import { OrgPermission } from '@/lib/api/types'
import { useAuth } from '@/lib/auth/AuthContext'
import { useT } from '@/lib/i18n/I18nContext'
import type { Dictionary } from '@/lib/i18n/en'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'

// Sections of the design whose API is not built yet. Each renders its designed first-use state (ComingSection); when the
// API ships, the route points at the real page and the section's feature flag turns on.
export type SectionKey = keyof Dictionary['sections']

const ICONS: Record<SectionKey, LucideIcon> = {
  apps: LayoutGrid,
  reviews: MessageSquare,
  compliance: CalendarClock,
  studio: Images,
  intake: CloudUpload,
  webhooks: Webhook,
  rules: BookOpen,
  developer: Terminal,
  adminRules: Radar,
  adminUsers: User,
  adminRoles: Shield,
  adminLogs: History,
  adminLanguages: Languages,
  adminFlags: Flag,
  adminSystem: Server,
}

export function SectionPage({ section }: { section: SectionKey }) {
  const t = useT()
  const { can } = useOrganization()
  const copy = t.sections[section]
  const Icon = ICONS[section]

  // Design 6.4: the first open of a new organization leads to the store connections, or says who can add one.
  const action =
    section === 'apps' ? (
      can(OrgPermission.ManageConnections) ? (
        <Link className="btn pri" to="/connections">
          {t.sections.apps.action}
        </Link>
      ) : (
        <span className="text-[13px] text-fg3">{t.sections.apps.askAdmin}</span>
      )
    ) : undefined

  return (
    <ComingSection
      title={copy.title}
      subtitle={copy.subtitle}
      emptyTitle={copy.emptyTitle}
      emptyBody={copy.emptyBody}
      icon={<Icon aria-hidden />}
      action={action}
    />
  )
}

/** Platform screens are for the installation's administrator only (Manager user type, design 17.26). */
export function PlatformPage({ section }: { section: Extract<SectionKey, `admin${string}`> }) {
  const t = useT()
  const { isManager } = useAuth()
  if (!isManager) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={t.states.forbiddenTitle}
        description={t.states.platformOnly}
        action={
          <Link className="btn inv" to={HOME_PATH}>
            {t.states.backToApps}
          </Link>
        }
      />
    )
  }
  return <SectionPage section={section} />
}
