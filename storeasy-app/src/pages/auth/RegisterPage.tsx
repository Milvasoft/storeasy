import { useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Lock } from 'lucide-react'
import { PasswordPolicy } from '@/components/auth/PasswordPolicy'
import { AuthLayout, AuthLead } from '@/components/layout/AuthLayout'
import { FormErrors } from '@/components/ui/Feedback'
import { StateView } from '@/components/ui/ScreenState'
import { Button, Field, Input, PasswordInput } from '@/components/ui/primitives'
import { registrationApi } from '@/lib/api/endpoints'
import { ApiError, apiErrorMessage, apiStatus, hasApiKey } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { emailError, passwordError, requiredError } from '@/lib/validation'

export const EMAIL_EXISTS_KEY = 'EmailExists'
const CLOSED_KEYS = ['RegistrationInviteOnly', 'RegistrationClosed']

interface Errors {
  name?: string | null
  surname?: string | null
  email?: string | null
  password?: string | null
}

/**
 * Create an account (design 5.10-5.13). Signing up does not sign in: the e-mail is verified first (5.14). An invitation
 * token from the invite link travels with the request so an invite-only installation accepts it.
 */
export function RegisterPage() {
  const t = useT()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const invitationToken = params.get('invite')
  const [form, setForm] = useState({ name: '', surname: '', email: '', password: '' })
  const [errors, setErrors] = useState<Errors>({})
  const [server, setServer] = useState<{ title: string; messages: string[]; exists?: boolean } | null>(null)
  const [closed, setClosed] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const errorRef = useRef<HTMLDivElement>(null)

  const set = (key: keyof typeof form) => (value: string) => setForm((current) => ({ ...current, [key]: value }))

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next: Errors = {
      name: requiredError(form.name, t.validation.nameRequired),
      surname: requiredError(form.surname, t.validation.surnameRequired),
      email: emailError(form.email, t),
      password: passwordError(form.password, t),
    }
    setErrors(next)
    if (Object.values(next).some(Boolean)) {
      return
    }
    setBusy(true)
    setServer(null)
    try {
      await registrationApi.register({
        Email: form.email.trim(),
        Name: form.name.trim(),
        Surname: form.surname.trim(),
        Password: form.password,
        InvitationToken: invitationToken,
      })
      navigate('/verify-email', { state: { email: form.email.trim() } })
    } catch (error) {
      if (apiStatus(error) === 403 && CLOSED_KEYS.some((key) => hasApiKey(error, key))) {
        setClosed(apiErrorMessage(error, t.register.closedBody))
        return
      }
      const exists = apiStatus(error) === 409 || hasApiKey(error, EMAIL_EXISTS_KEY)
      const messages = error instanceof ApiError ? error.messages : []
      setServer(
        exists
          ? { title: t.register.exists, messages: [], exists: true }
          : {
              title:
                messages.length > 1
                  ? interpolate(t.register.failedMany, { count: messages.length })
                  : apiErrorMessage(error, t.common.error),
              messages: messages.length > 1 ? messages : [],
            },
      )
      if (exists) {
        setErrors((current) => ({ ...current, email: t.register.emailInUse }))
      }
      window.setTimeout(() => errorRef.current?.focus(), 0)
    } finally {
      setBusy(false)
    }
  }

  if (closed) {
    return (
      <AuthLayout>
        <StateView
          icon={<Lock aria-hidden />}
          title={t.register.closedTitle}
          description={closed}
          action={
            <Link className="btn inv" to="/login">
              {t.login.submit}
            </Link>
          }
        />
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      <AuthLead title={t.register.title} lead={t.register.lead} />
      <form className="grid gap-4" onSubmit={submit} noValidate>
        {server ? (
          <FormErrors
            ref={errorRef}
            title={server.title}
            messages={server.messages}
            detail={
              server.exists ? (
                <>
                  <Link className="lnk" to="/login">
                    {t.login.submit}
                  </Link>{' '}
                  ·{' '}
                  <Link className="lnk" to="/forgot-password" state={{ email: form.email.trim() }}>
                    {t.login.forgot}
                  </Link>
                </>
              ) : undefined
            }
          />
        ) : null}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t.fields.name} error={errors.name}>
            <Input
              autoComplete="given-name"
              value={form.name}
              maxLength={TEXT_LIMITS.personName}
              onChange={(event) => set('name')(event.target.value)}
            />
          </Field>
          <Field label={t.fields.surname} error={errors.surname}>
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
        {errors.password ? <PasswordPolicy value={form.password} /> : null}
        <p className="m-0 text-xs text-fg3">{t.register.openSource}</p>
        <Button type="submit" variant="primary" size="lg" block loading={busy}>
          {t.register.submit}
        </Button>
        <p className="m-0 text-center text-[12.5px] text-fg2">
          {t.register.haveAccount}{' '}
          <Link className="lnk" to="/login">
            {t.login.submit}
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}
