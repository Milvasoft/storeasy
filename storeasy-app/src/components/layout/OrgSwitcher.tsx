import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Check, ChevronsUpDown, Plus, Settings } from 'lucide-react'
import { CreateOrganizationForm } from '@/components/org/CreateOrganizationForm'
import { useDeployment } from '@/features/access/publicSettings'
import { Menu, type MenuEntry } from '@/components/ui/Menu'
import { Modal } from '@/components/ui/Modal'
import { foldSearch } from '@/components/ui/Select'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { roleLabel } from '@/lib/org/roles'
import { showToast } from '@/lib/toast/toast'

// The search box shows only above this many organizations (design 5.26).
const SEARCH_FROM = 6

function initialOf(name: string): string {
  return name.trim().slice(0, 1).toLocaleUpperCase('tr') || '?'
}

/** The organization switcher at the top of the menu (design 5.26): the list, "New organization", the settings link. */
export function OrgSwitcher({ onNavigate }: { onNavigate?: () => void }) {
  const t = useT()
  const navigate = useNavigate()
  const { organizations, current, switchTo } = useOrganization()
  const [creating, setCreating] = useState(false)
  const [query, setQuery] = useState('')
  const { singleTeam } = useDeployment()

  if (!current) {
    return null
  }

  // A single team instance has one organization: the team's name, no switcher and no "new organization".
  if (singleTeam) {
    return (
      <div className="orgsw" role="group" aria-label={interpolate(t.orgSwitcher.team, { name: current.Name })}>
        <span className="oav">{initialOf(current.Name)}</span>
        <span className="min-w-0 flex-1">
          <b className="truncate">{current.Name}</b>
          <small>{roleLabel(current.Role, t)}</small>
        </span>
      </div>
    )
  }

  const needle = foldSearch(query.trim())
  const visible = needle ? organizations.filter((org) => foldSearch(`${org.Name} ${org.Slug}`).includes(needle)) : organizations

  const entries: MenuEntry[] = [
    { key: 'heading', heading: interpolate(t.orgSwitcher.heading, { count: organizations.length }) },
    ...(organizations.length >= SEARCH_FROM
      ? [
          {
            key: 'search',
            content: (
              <input
                className="inp mb-1 !min-h-[34px]"
                aria-label={t.orgSwitcher.search}
                placeholder={t.orgSwitcher.search}
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            ),
          },
        ]
      : []),
    ...visible.map((org) => ({
      key: `org-${org.Id}`,
      checked: org.Id === current.Id,
      icon: <span className="oav !h-[26px] !w-[26px] !rounded-sm !text-xs">{initialOf(org.Name)}</span>,
      label: (
        <span className="flex items-center gap-2">
          <span className="min-w-0 flex-1">
            <span className="block truncate font-medium">{org.Name}</span>
            <small className="block text-[11px] text-fg3">{roleLabel(org.Role, t)}</small>
          </span>
          {org.Id === current.Id ? <Check className="h-4 w-4 text-accent" aria-hidden /> : null}
        </span>
      ),
      onSelect: () => {
        if (org.Id !== current.Id) {
          switchTo(org.Id)
          showToast(interpolate(t.orgSwitcher.switched, { name: org.Name }), 'info')
        }
        onNavigate?.()
      },
    })),
    { key: 'separator', separator: true },
    { key: 'new', icon: <Plus aria-hidden />, label: t.orgSwitcher.create, onSelect: () => setCreating(true) },
    {
      key: 'settings',
      icon: <Settings aria-hidden />,
      label: t.orgSwitcher.settings,
      onSelect: () => {
        navigate('/settings')
        onNavigate?.()
      },
    },
  ]

  return (
    <>
      <Menu
        label={t.orgSwitcher.label}
        placement="bottom-start"
        className="w-[300px]"
        trigger={
          <button type="button" className="orgsw" aria-label={interpolate(t.orgSwitcher.current, { name: current.Name })}>
            <span className="oav">{initialOf(current.Name)}</span>
            <span className="min-w-0 flex-1">
              <b className="truncate">{current.Name}</b>
              <small>{roleLabel(current.Role, t)}</small>
            </span>
            <ChevronsUpDown className="h-4 w-4 flex-none text-fg3" aria-hidden />
          </button>
        }
        entries={entries}
      />
      <Modal open={creating} onClose={() => setCreating(false)} title={t.organization.newTitle} description={t.organization.newSubtitle}>
        <CreateOrganizationForm
          onCancel={() => setCreating(false)}
          onCreated={(organizationId) => {
            setCreating(false)
            switchTo(organizationId)
            showToast(t.organization.created, 'success')
            navigate('/apps')
            onNavigate?.()
          }}
        />
      </Modal>
    </>
  )
}
