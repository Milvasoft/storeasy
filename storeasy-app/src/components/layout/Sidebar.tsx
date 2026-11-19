import { Link, NavLink } from 'react-router-dom'
import { Badge } from '@/components/ui/Badge'
import { APP_VERSION, SOURCE_URL } from '@/config'
import { useActivity } from '@/lib/activity/ActivityContext'
import { activeJobCount } from '@/lib/activity/hatState'
import { useAuth } from '@/lib/auth/AuthContext'
import { useGlobalPermissions } from '@/lib/auth/globalPermissions'
import { useFeature, type FeatureFlagKey } from '@/lib/features/FeatureFlagsContext'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { navGroups, navLabel, HOME_PATH, type NavItem } from '@/lib/nav/nav'
import { useDeployment } from '@/features/access/publicSettings'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { cn } from '@/lib/utils/cn'
import { Brand } from './Logo'
import { OrgSwitcher } from './OrgSwitcher'
import { UserMenu } from './UserMenu'

function ComingMark({ feature }: { feature?: FeatureFlagKey }) {
  const t = useT()
  // Hooks run in the same order every render: the flag is read even for items that have none.
  const enabled = useFeature(feature ?? 'Platform')
  if (!feature || enabled) {
    return null
  }
  return (
    <span className="ml-auto" title={t.coming.badge}>
      <Badge small>{t.coming.short}</Badge>
    </span>
  )
}

function NavEntry({ item, onNavigate, liveCount }: { item: NavItem; onNavigate?: () => void; liveCount: number }) {
  const t = useT()
  const Icon = item.icon
  return (
    <NavLink to={item.path} onClick={onNavigate} className={({ isActive }) => cn('nav-i', isActive && 'on')}>
      <Icon aria-hidden />
      <span className="min-w-0 truncate">{navLabel(item.key, t)}</span>
      {item.key === 'activity' && liveCount > 0 ? (
        <span className="cnt live mono text-[11px] font-semibold" aria-label={interpolate(t.activity.runningJobs, { count: liveCount })}>
          ● {liveCount}
        </span>
      ) : (
        <ComingMark feature={item.feature} />
      )}
    </NavLink>
  )
}

/** The menu: logo, organization switcher, groups filtered by permission, the person and the license line. */
export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const t = useT()
  const { can } = useOrganization()
  const { isManager } = useAuth()
  const global = useGlobalPermissions()
  const { state } = useActivity()
  const liveCount = activeJobCount(state)
  const { singleTeam } = useDeployment()

  return (
    <>
      <Link to={HOME_PATH} className="brand" onClick={onNavigate} aria-label={t.nav.home}>
        <Brand className="!p-0" />
      </Link>
      <OrgSwitcher onNavigate={onNavigate} />
      <nav aria-label={t.nav.label} className="grid gap-0.5">
        {navGroups(singleTeam)
          .filter((group) => !group.managerOnly || isManager)
          .map((group) => {
            const items = group.items.filter(
              (item) => (!item.permission || can(item.permission)) && (!item.globalPermission || global.has(item.globalPermission)),
            )
            if (items.length === 0) {
              return null
            }
            return (
              <div key={group.key} className="grid gap-0.5">
                {group.key !== 'main' ? <div className={cn('nav-g', group.nested && 'pl-3')}>{t.nav.groups[group.key]}</div> : null}
                {items.map((item) => (
                  <NavEntry key={item.key} item={item} onNavigate={onNavigate} liveCount={liveCount} />
                ))}
              </div>
            )
          })}
      </nav>
      <div className="side-f">
        <UserMenu onNavigate={onNavigate} />
        <div className="lic">
          <span>
            {t.footer.license} ·{' '}
            <a
              href={SOURCE_URL}
              target="_blank"
              rel="noopener noreferrer"
              className="underline decoration-fg3/40 underline-offset-2 hover:text-fg"
            >
              {t.footer.sourceCode}
            </a>
          </span>
          <span>v{APP_VERSION}</span>
        </div>
      </div>
    </>
  )
}
