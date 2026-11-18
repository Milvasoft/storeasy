import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { Archive, ShieldAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { StateView } from '@/components/ui/ScreenState'
import { Button, Field, Input, PageHeader } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, hasApiKey } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { OrgPermission, OrgRole } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { isValidSlug, toSlug, toSlugDraft } from '@/lib/utils/slug'

export const SLUG_TAKEN_KEY = 'OrganizationSlugTaken'
const MAX_SLUG_LENGTH = 40

/**
 * Organization settings (design 15.20-15.22). The owner edits the name and the short name (PUT organization; only a
 * changed short name is sent). Changing the short name breaks addresses that use it, which the warning says; a taken
 * one comes back as 409 and stays in the form with suggestions. Retention settings come with their endpoint.
 */
export function OrgSettingsPage() {
  const t = useT()
  const organization = useCurrentOrganization()
  const { can, refetch } = useOrganization()
  const retention = useFeature('OrganizationRetention')
  const editable = can(OrgPermission.ManageOrganization)
  const [name, setName] = useState(organization.Name)
  const [slug, setSlug] = useState(organization.Slug)
  const [slugFocused, setSlugFocused] = useState(false)
  const [errors, setErrors] = useState<{ name?: string | null; slug?: string | null }>({})
  const [taken, setTaken] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  useEffect(() => {
    setName(organization.Name)
    setSlug(organization.Slug)
  }, [organization.Name, organization.Slug])

  const nextSlug = toSlug(slug)
  const slugChanged = nextSlug !== organization.Slug

  const save = useMutation({
    mutationFn: () => organizationsApi.update(organization.Id, name.trim(), slugChanged ? nextSlug : null),
    meta: { silentError: true },
    onSuccess: async () => {
      setTaken(null)
      setServer(null)
      await refetch()
      showToast(t.settings.saved, 'success')
    },
    onError: (failure) => {
      if (hasApiKey(failure, SLUG_TAKEN_KEY)) {
        setTaken(nextSlug)
        setServer(t.settings.slugTaken)
      } else {
        setServer(apiErrorMessage(failure, t.common.error))
      }
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

  const changes = (name.trim() !== organization.Name ? 1 : 0) + (slugChanged ? 1 : 0)
  const reset = () => {
    setName(organization.Name)
    setSlug(organization.Slug)
    setErrors({})
    setTaken(null)
    setServer(null)
  }
  const submit = (event: FormEvent) => {
    event.preventDefault()
    const next = {
      name: name.trim() ? null : t.organization.nameRequired,
      slug: isValidSlug(nextSlug) && nextSlug.length <= MAX_SLUG_LENGTH ? null : t.organization.slugInvalid,
    }
    setErrors(next)
    if (!next.name && !next.slug && changes > 0) {
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
            {server ? <FormErrors title={server} /> : null}
            <Field label={t.organization.name} error={errors.name}>
              <Input
                value={name}
                maxLength={TEXT_LIMITS.organizationName}
                readOnly={!editable}
                onChange={(event) => setName(event.target.value)}
              />
            </Field>
            <Field
              label={t.organization.slug}
              hint={t.settings.slugHint}
              error={errors.slug ?? (taken && taken === nextSlug ? interpolate(t.settings.slugTakenField, { slug: taken }) : null)}
            >
              <Input
                mono
                value={slug}
                readOnly={!editable}
                maxLength={MAX_SLUG_LENGTH}
                autoComplete="off"
                spellCheck={false}
                onFocus={() => setSlugFocused(true)}
                onBlur={() => setSlug(toSlug(slug) || slug)}
                onChange={(event) => setSlug(toSlugDraft(event.target.value))}
              />
            </Field>
            {taken && taken === nextSlug ? (
              <div className="flex flex-wrap items-center gap-2 text-[13px]">
                {[`${taken}-2`, `${taken}-${toSlug(t.organization.suggestionSuffix)}`].map((option) => (
                  <button key={option} type="button" className="chip" onClick={() => setSlug(option)}>
                    {option}
                  </button>
                ))}
              </div>
            ) : null}
            {editable && (slugFocused || slugChanged) ? (
              <Callout tone="warning">{interpolate(t.settings.slugWarning, { slug: organization.Slug })}</Callout>
            ) : null}
          </div>
          {editable ? (
            <div className="card-f justify-end">
              {changes > 0 ? <span className="mr-auto text-xs text-fg3">{interpolate(t.settings.changes, { count: changes })}</span> : null}
              <Button variant="ghost" disabled={changes === 0} onClick={reset}>
                {t.common.cancel}
              </Button>
              <Button type="submit" variant="primary" loading={save.isPending} disabled={changes === 0}>
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
