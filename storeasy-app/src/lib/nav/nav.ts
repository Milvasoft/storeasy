import {
  Activity,
  BookOpen,
  CalendarClock,
  CloudUpload,
  Flag,
  History,
  Images,
  Languages,
  LayoutGrid,
  List,
  MessageSquare,
  Plug,
  Radar,
  Server,
  Settings,
  Shield,
  Terminal,
  User,
  Users,
  Webhook,
  type LucideIcon,
} from 'lucide-react'
import { OrgPermission } from '@/lib/api/types'
import type { FeatureFlagKey } from '@/lib/features/FeatureFlagsContext'
import type { Dictionary } from '@/lib/i18n/en'

// The app's menu, as data (design kit NAV table, 19 section 7). Labels come from the dictionary; an item that needs a
// permission is hidden from roles without it (Editor and Viewer do not see connections and webhooks); an item whose
// section is not built yet still shows, marked "coming", and opens its designed empty state.
export type NavKey =
  | 'apps'
  | 'activity'
  | 'reviews'
  | 'compliance'
  | 'studio'
  | 'intake'
  | 'connections'
  | 'webhooks'
  | 'members'
  | 'audit'
  | 'rules'
  | 'settings'
  | 'team'
  | 'developer'
  | 'adminRules'
  | 'adminUsers'
  | 'adminRoles'
  | 'adminLogs'
  | 'adminLanguages'
  | 'adminFlags'
  | 'adminSystem'

export interface NavItem {
  key: NavKey
  path: string
  icon: LucideIcon
  permission?: OrgPermission
  // A platform (global) permission, "Group.Action" (design 17.27 menu map); App.SuperAdmin sees every item.
  globalPermission?: string
  feature?: FeatureFlagKey
}

export interface NavGroup {
  key: 'main' | 'delivery' | 'organization' | 'developer' | 'platform' | 'settings' | 'system'
  items: NavItem[]
  // The Platform group shows only to platform administrators (Manager user type).
  managerOnly?: boolean
  // Drawn as a part of the group above it (single team: System under Settings).
  nested?: boolean
}

export const NAV: NavGroup[] = [
  {
    key: 'main',
    items: [
      { key: 'apps', path: '/apps', icon: LayoutGrid },
      { key: 'activity', path: '/activity', icon: Activity },
      { key: 'reviews', path: '/reviews', icon: MessageSquare, feature: 'Reviews' },
      { key: 'compliance', path: '/compliance', icon: CalendarClock, feature: 'Compliance' },
      { key: 'studio', path: '/studio', icon: Images, feature: 'Studio' },
    ],
  },
  {
    key: 'delivery',
    items: [
      { key: 'intake', path: '/intake', icon: CloudUpload, feature: 'Intake' },
      { key: 'connections', path: '/connections', icon: Plug, permission: OrgPermission.ManageConnections },
      { key: 'webhooks', path: '/webhooks', icon: Webhook, permission: OrgPermission.ManageWebhooks, feature: 'Webhooks' },
    ],
  },
  {
    key: 'organization',
    items: [
      { key: 'members', path: '/members', icon: Users },
      { key: 'audit', path: '/audit', icon: List },
      { key: 'rules', path: '/rules', icon: BookOpen, feature: 'Rules' },
      { key: 'settings', path: '/settings', icon: Settings },
    ],
  },
  {
    key: 'developer',
    items: [{ key: 'developer', path: '/developer', icon: Terminal, feature: 'Developer' }],
  },
  {
    key: 'platform',
    managerOnly: true,
    items: [
      { key: 'adminRules', path: '/admin/rules', icon: Radar, feature: 'RuleWatcher', globalPermission: 'RuleCatalogManagement.List' },
      { key: 'adminUsers', path: '/admin/users', icon: User, feature: 'Platform', globalPermission: 'UserManagement.List' },
      { key: 'adminRoles', path: '/admin/roles', icon: Shield, feature: 'Platform', globalPermission: 'RoleManagement.List' },
      {
        key: 'adminLogs',
        path: '/admin/activity-logs',
        icon: History,
        feature: 'Platform',
        globalPermission: 'ActivityLogManagement.List',
      },
      {
        key: 'adminLanguages',
        path: '/admin/languages',
        icon: Languages,
        feature: 'Platform',
        globalPermission: 'LanguageManagement.List',
      },
      {
        key: 'adminFlags',
        path: '/admin/feature-flags',
        icon: Flag,
        feature: 'Platform',
        globalPermission: 'FeatureFlagManagement.List',
      },
      { key: 'adminSystem', path: '/admin/system', icon: Server, feature: 'Platform', globalPermission: 'SystemManagement.Status' },
    ],
  },
]

function navItem(key: NavKey): NavItem {
  const item = NAV.flatMap((group) => group.items).find((candidate) => candidate.key === key)
  if (!item) {
    throw new Error(`Unknown menu item ${key}`)
  }
  return item
}

// A single team instance (Storeasy:Mode=SingleTeam) has one organization, "the team", and its administrator is also the
// platform administrator: one Settings area holds the team's settings and, for the administrator, the System items,
// instead of an organization world and a platform world side by side. The routes stay the same.
export const SINGLE_TEAM_NAV: NavGroup[] = [
  NAV[0],
  { key: 'delivery', items: [navItem('intake'), navItem('webhooks')] },
  NAV[3],
  {
    key: 'settings',
    items: [
      { key: 'team', path: '/settings', icon: Settings },
      navItem('members'),
      navItem('connections'),
      navItem('audit'),
      navItem('rules'),
    ],
  },
  {
    key: 'system',
    managerOnly: true,
    nested: true,
    items: ['adminSystem', 'adminUsers', 'adminRoles', 'adminLanguages', 'adminFlags', 'adminLogs', 'adminRules'].map((key) =>
      navItem(key as NavKey),
    ),
  },
]

/** The menu of the deployment mode. */
export function navGroups(singleTeam: boolean): NavGroup[] {
  return singleTeam ? SINGLE_TEAM_NAV : NAV
}

export const NAV_ITEMS: NavItem[] = NAV.flatMap((group) => group.items)

export function navLabel(key: NavKey, t: Dictionary): string {
  return t.nav[key]
}

// The menu item a path belongs to ("/activity/jobs" -> activity), for the breadcrumb.
export function navItemFor(pathname: string): NavItem | null {
  return (
    NAV_ITEMS.filter((item) => pathname === item.path || pathname.startsWith(`${item.path}/`)).sort(
      (a, b) => b.path.length - a.path.length,
    )[0] ?? null
  )
}

// The first screen after sign-in: the apps list (design 5.1). A new organization sees its first-use state there, which
// leads to the store connections.
export const HOME_PATH = '/apps'
