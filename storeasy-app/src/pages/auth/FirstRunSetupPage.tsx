import { useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { PasswordPolicy } from '@/components/auth/PasswordPolicy'
import { AuthLayout, AuthLead } from '@/components/layout/AuthLayout'
import { FormErrors } from '@/components/ui/Feedback'
import { Button, Field, Input, PasswordInput } from '@/components/ui/primitives'
import { ApiError, apiErrorMessage, apiStatus } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { useAuth } from '@/lib/auth/AuthContext'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { emailError, passwordError, requiredError } from '@/lib/validation'

interface Errors {
  code?: string | null
  name?: string | null
  email?: string | null
  password?: string | null
  team?: string | null
}

type Problem = { title: string; detail?: string; messages?: string[] }

/**
 * First run of a single team instance (system/setup): while the server has no user, the visitor with the one time setup
 * code from the API log creates the administrator and the team and is signed in. Shown at /login (and so at /, which
 * sends a guest there) instead of the sign-in until the setup is done.
 */
export function FirstRunSetupPage() {
  const t = useT()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { completeSetup } = useAuth()
  const [form, setForm] = useState({ code: '', name: '', surname: '', email: '', password: '', team: '' })
  const [errors, setErrors] = useState<Errors>({})
  const [problem, setProblem] = useState<Problem | null>(null)
  const [busy, setBusy] = useState(false)
  const errorRef = useRef<HTMLDivElement>(null)

  const set = (key: keyof typeof form) => (value: string) => setForm((current) => ({ ...current, [key]: value }))

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next: Errors = {
      code: requiredError(form.code, t.firstRun.codeRequired),
      name: requiredError(form.name, t.validation.nameRequired),
      email: emailError(form.email, t),
      password: passwordError(form.password, t),
      team: requiredError(form.team, t.firstRun.teamRequired),
    }
    setErrors(next)
    if (Object.values(next).some(Boolean)) {
      return
    }
    setBusy(true)
    setProblem(null)
    try {
      await completeSetup({
        SetupCode: form.code.trim(),
        Name: form.name.trim(),
        Surname: form.surname.trim() || null,
        Email: form.email.trim(),
        Password: form.password,
        OrganizationName: form.team.trim(),
      })
      navigate(HOME_PATH, { replace: true })
    } catch (error) {
      const status = apiStatus(error)
      if (status === 403) {
        setProblem({ title: t.firstRun.codeWrong, detail: t.firstRun.codeWrongDetail })
        setErrors((current) => ({ ...current, code: t.firstRun.codeWrong }))
      } else if (status === 409 || status === 404) {
        // Someone finished the setup meanwhile (or this is not a single team instance): the sign-in takes over.
        setProblem({ title: t.firstRun.alreadyDone, detail: t.firstRun.alreadyDoneDetail })
        void queryClient.invalidateQueries({ queryKey: ['system', 'public-settings'] })
      } else {
        const messages = error instanceof ApiError ? error.messages : []
        setProblem(
          messages.length > 1
            ? { title: interpolate(t.firstRun.failedMany, { count: messages.length }), messages }
            : { title: apiErrorMessage(error, t.common.error) },
        )
      }
      window.setTimeout(() => errorRef.current?.focus(), 0)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout>
      <AuthLead title={t.firstRun.title} lead={t.firstRun.lead} />
      <form className="grid gap-4" onSubmit={submit} noValidate aria-label={t.firstRun.label}>
        {problem ? <FormErrors ref={errorRef} title={problem.title} detail={problem.detail} messages={problem.messages ?? []} /> : null}
        <Field label={t.firstRun.code} error={errors.code} hint={t.firstRun.codeHint}>
          <Input
            className="mono"
            autoComplete="one-time-code"
            spellCheck={false}
            value={form.code}
            maxLength={200}
            onChange={(event) => set('code')(event.target.value)}
          />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t.fields.name} error={errors.name}>
            <Input
              autoComplete="given-name"
              value={form.name}
              maxLength={TEXT_LIMITS.personName}
              onChange={(event) => set('name')(event.target.value)}
            />
          </Field>
          <Field label={t.fields.surname} optional>
            <Input
              autoComplete="family-name"
              value={form.surname}
              maxLength={TEXT_LIMITS.personName}
              onChange={(event) => set('surname')(event.target.value)}
            />
          </Field>
        </div>
        <Field label={t.fields.email} error={errors.email}>
          <Input
            type="email"
            autoComplete="email"
            inputMode="email"
            value={form.email}
            maxLength={TEXT_LIMITS.email}
            onChange={(event) => set('email')(event.target.value)}
          />
        </Field>
        <Field label={t.fields.password} error={errors.password} hint={<PasswordPolicy value={form.password} />}>
          <PasswordInput
            autoComplete="new-password"
            value={form.password}
            maxLength={TEXT_LIMITS.password}
            onChange={(event) => set('password')(event.target.value)}
          />
        </Field>
        <Field label={t.firstRun.team} error={errors.team} hint={t.firstRun.teamHint}>
          <Input value={form.team} maxLength={TEXT_LIMITS.organizationName} onChange={(event) => set('team')(event.target.value)} />
        </Field>
        <p className="m-0 text-xs text-fg3">{t.firstRun.adminNote}</p>
        <Button type="submit" variant="primary" size="lg" block loading={busy}>
          {t.firstRun.submit}
        </Button>
      </form>
    </AuthLayout>
  )
}
