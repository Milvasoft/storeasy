import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { FormErrors } from '@/components/ui/Feedback'
import { Button, Field, Input } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, apiStatus } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { useT } from '@/lib/i18n/I18nContext'
import { queryKeys } from '@/lib/query/keys'
import { isValidSlug, toSlug, toSlugDraft } from '@/lib/utils/slug'

/**
 * Name and slug of a new organization (design 5.27, 5.28). The slug follows the name until the user edits it; a taken
 * slug comes back as 409 and stays in the form with a suggestion.
 */
export function CreateOrganizationForm({
  onCreated,
  onCancel,
  submitLabel,
}: {
  onCreated: (organizationId: number) => void
  onCancel?: () => void
  submitLabel?: string
}) {
  const t = useT()
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [slugEdited, setSlugEdited] = useState(false)
  const [errors, setErrors] = useState<{ name?: string; slug?: string }>({})
  const [serverError, setServerError] = useState<{ title: string; messages: string[]; conflict: boolean } | null>(null)

  const create = useMutation({
    mutationFn: () => organizationsApi.create({ Name: name.trim(), Slug: toSlug(slug) }),
    meta: { silentError: true },
    onSuccess: async (organizationId) => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all })
      onCreated(organizationId)
    },
    onError: (error) => {
      const conflict = apiStatus(error) === 409
      setServerError({
        title: conflict ? t.organization.slugTakenTitle : apiErrorMessage(error, t.common.error),
        messages: conflict ? [apiErrorMessage(error, t.organization.slugTakenBody)] : [],
        conflict,
      })
    },
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const next = {
      name: name.trim() ? undefined : t.organization.nameRequired,
      slug: isValidSlug(toSlug(slug)) ? undefined : t.organization.slugInvalid,
    }
    setErrors(next)
    setServerError(null)
    if (next.name || next.slug) {
      return
    }
    create.mutate()
  }

  return (
    <form className="grid gap-4" onSubmit={submit} noValidate>
      {serverError ? <FormErrors title={serverError.title} messages={serverError.messages} /> : null}
      <Field label={t.organization.name} error={errors.name}>
        <Input
          value={name}
          maxLength={TEXT_LIMITS.organizationName}
          autoComplete="organization"
          onChange={(event) => {
            setName(event.target.value)
            if (!slugEdited) {
              setSlug(toSlug(event.target.value))
            }
          }}
        />
      </Field>
      <Field
        label={t.organization.slug}
        error={errors.slug ?? (serverError?.conflict ? t.organization.slugTakenField : null)}
        hint={t.organization.slugHint}
      >
        <Input
          mono
          value={slug}
          maxLength={TEXT_LIMITS.organizationName}
          spellCheck={false}
          onChange={(event) => {
            setSlugEdited(true)
            setSlug(toSlugDraft(event.target.value))
          }}
        />
      </Field>
      {serverError?.conflict ? (
        <div className="flex flex-wrap items-center gap-2 text-xs text-fg3">
          {t.organization.suggestion}
          {[`${slug}-2`, `${slug}-${toSlug(t.organization.suggestionSuffix)}`].map((option) => (
            <button key={option} type="button" className="chip" onClick={() => setSlug(option)}>
              {option}
            </button>
          ))}
        </div>
      ) : null}
      <p className="text-[13px] text-fg2">{t.organization.ownerNote}</p>
      <div className="flex flex-wrap justify-end gap-2">
        {onCancel ? (
          <Button variant="ghost" onClick={onCancel}>
            {t.common.cancel}
          </Button>
        ) : null}
        <Button type="submit" variant="primary" loading={create.isPending}>
          {submitLabel ?? t.organization.create}
        </Button>
      </div>
    </form>
  )
}
