import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { Archive, ShieldAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { StateView } from '@/components/ui/ScreenState'
import { Button, Field, Input, PageHeader } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { OrgPermission, OrgRole } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useT } from '@/lib/i18n/I18nContext'
import { HOME_PATH } from '@/lib/nav/nav'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'

/**
 * Organization settings (design 15.20-15.22). The name is editable by the owner (PUT organization); the slug cannot
 * change yet (backend gap) and is shown read-only; retention settings come with their endpoint.
 */
export function OrgSettingsPage() {
  const t = useT()
  const organization = useCurrentOrganization()
  const { can, refetch } = useOrganization()
  const retention = useFeature('OrganizationRetention')
  const editable = can(OrgPermission.ManageOrganization)
  const [name, setName] = useState(organization.Name)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setName(organization.Name)
  }, [organization.Name])

  const save = useMutation({
    mutationFn: () => organizationsApi.update(organization.Id, name.trim()),
    onSuccess: async () => {
      await refetch()
      showToast(t.settings.saved, 'success')
    },
  })

  // Design 15.22: a viewer has no business here; an admin sees the page read only.
  if (organization.Role === OrgRole.Viewer) {
    return (
      <StateView
        icon={<ShieldAlert aria-hidden />}
        title={t.states.forbiddenTitle}
        description={t.settings.forbidden}
        action={
          <Link className="btn inv" to={HOME_PATH}>
            {t.states.backToApps}
          </Link>
        }
      />
    )
  }

  const changed = name.trim() !== organization.Name
  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = name.trim() ? null : t.organization.nameRequired
    setError(problem)
    if (!problem && changed) {
      save.mutate()
    }
  }

  return (
    <>
      <PageHeader title={t.settings.title} subtitle={t.settings.subtitle} />
      <div className="grid max-w-3xl gap-5">
        {!editable ? <Callout tone="info">{t.settings.readOnly}</Callout> : null}
        <form className="card" onSubmit={submit} noValidate>
          <div className="card-h">
            <h2 className="h3">{t.settings.general}</h2>
          </div>
          <div className="card-b grid gap-4">
            <Field label={t.organization.name} error={error}>
              <Input
                value={name}
                maxLength={TEXT_LIMITS.organizationName}
                readOnly={!editable}
                onChange={(event) => setName(event.target.value)}
              />
            </Field>
            <Field label={t.organization.slug} hint={t.settings.slugLocked}>
              <Input mono value={organization.Slug} readOnly />
            </Field>
          </div>
          {editable ? (
            <div className="card-f justify-end">
              <Button variant="ghost" disabled={!changed} onClick={() => setName(organization.Name)}>
                {t.common.cancel}
              </Button>
              <Button type="submit" variant="primary" loading={save.isPending} disabled={!changed}>
                {t.common.save}
              </Button>
            </div>
          ) : null}
        </form>
        {!retention ? (
          <section className="card dash">
            <div className="card-h">
              <Archive className="h-4 w-4 text-fg3" aria-hidden />
              <h2 className="h3">{t.settings.retentionTitle}</h2>
              <Badge small>{t.coming.short}</Badge>
            </div>
            <p className="card-b m-0 text-[13px] text-fg2">{t.settings.retentionBody}</p>
          </section>
        ) : null}
      </div>
    </>
  )
}
