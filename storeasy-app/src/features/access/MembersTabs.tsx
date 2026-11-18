import { Tabs } from '@/components/ui/Tabs'
import { useAccessText } from './i18n'

/** Members and roles share one tab row (design 15.13). */
export function MembersTabs({ members }: { members?: number }) {
  const t = useAccessText()
  return (
    <Tabs
      label={t.roles.tabsLabel}
      items={[
        { value: 'members', label: t.roles.tabMembers, to: '/members', count: members },
        { value: 'roles', label: t.roles.tabRoles, to: '/members/roles' },
      ]}
    />
  )
}
