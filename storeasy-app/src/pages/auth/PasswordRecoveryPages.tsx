import { useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { KeyRound, Link2Off, MailCheck } from 'lucide-react'
import { PasswordPolicy } from '@/components/auth/PasswordPolicy'
import { AuthCard, AuthLayout, AuthLead, SoloLayout } from '@/components/layout/AuthLayout'
import { FormErrors } from '@/components/ui/Feedback'
import { Button, Field, Input, PasswordInput } from '@/components/ui/primitives'
import { registrationApi } from '@/lib/api/endpoints'
import { ApiError, apiErrorMessage, apiStatus, hasApiKey } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { formatCountdown, useCountdown } from '@/lib/hooks/useCountdown'
import { useT } from '@/lib/i18n/I18nContext'
import { withPlaceholders } from '@/lib/i18n/interpolate'
import { emailError, passwordError } from '@/lib/validation'

// MessageKey.AccountTokenInvalid: an expired, used or replaced reset link.
export const RESET_TOKEN_INVALID_KEY = 'AccountTokenInvalid'
const RESEND_SECONDS = 60

/**
 * "Forgot your password?" (design 5.17, 5.18). The answer never says whether the address has an account: the sent
 * screen speaks of "an account, if there is one". Resend counts down 60 s; 429 starts the countdown from Retry-After.
 */
export function ForgotPasswordPage() {
  const t = useT()
  const location = useLocation()
  const [email, setEmail] = useState((location.state as { email?: string } | null)?.email ?? '')
  const [error, setError] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)
  const [sentTo, setSentTo] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const countdown = useCountdown()

  const send = async (address: string) => {
    setBusy(true)
    setServerError(null)
    try {
      await registrationApi.forgotPassword(address)
      setSentTo(address)
      countdown.start(RESEND_SECONDS)
    } catch (failure) {
      if (apiStatus(failure) === 429) {
        countdown.start((failure as ApiError).retryAfterSeconds ?? RESEND_SECONDS)
      }
      setServerError(apiErrorMessage(failure, t.common.error))
    } finally {
      setBusy(false)
    }
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = emailError(email, t)
    setError(problem)
    if (!problem) {
      void send(email.trim())
    }
  }

  if (sentTo) {
    return (
      <SoloLayout>
        <AuthCard icon={<MailCheck aria-hidden />} title={t.forgot.sentTitle}>
          <p className="m-0 text-sm text-fg2">{withPlaceholders(t.forgot.sentBody, { email: <b className="text-fg">{sentTo}</b> })}</p>
          {serverError ? <FormErrors title={serverError} /> : null}
          <div className="flex flex-wrap justify-center gap-2">
            <Button variant="ghost" onClick={() => setSentTo(null)}>
              {t.forgot.otherAddress}
            </Button>
            <Button variant="inverse" loading={busy} disabled={countdown.seconds > 0} onClick={() => void send(sentTo)}>
              {countdown.seconds > 0 ? `${t.forgot.resend} · ${formatCountdown(countdown.seconds)}` : t.forgot.resend}
            </Button>
          </div>
          <Link className="lnk text-[13px]" to="/login">
            {t.forgot.backToLogin}
          </Link>
        </AuthCard>
      </SoloLayout>
    )
  }

  return (
    <AuthLayout>
      <AuthLead title={t.forgot.title} lead={t.forgot.lead} />
      <form className="grid gap-4" onSubmit={submit} noValidate>
        {serverError ? (
          <FormErrors title={serverError} detail={countdown.seconds > 0 ? formatCountdown(countdown.seconds) : undefined} />
        ) : null}
        <Field label={t.fields.email} error={error}>
          <Input
            type="email"
            autoComplete="email"
            inputMode="email"
            value={email}
            maxLength={TEXT_LIMITS.email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </Field>
        <Button type="submit" variant="primary" size="lg" block loading={busy} disabled={countdown.seconds > 0}>
          {t.forgot.submit}
        </Button>
        <Link className="lnk justify-self-center text-[13px]" to="/login">
          {t.forgot.backToLogin}
        </Link>
      </form>
    </AuthLayout>
  )
}

/**
 * "Set a new password" from the reset link (design 5.19-5.21). The repeat field is compared here only; on success every
 * session ends and the sign-in screen says so. An expired, used or replaced link has one text (5.20).
 */
export function ResetPasswordPage() {
  const t = useT()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [errors, setErrors] = useState<{ password?: string | null; repeat?: string | null }>({})
  const [server, setServer] = useState<{ title: string; messages: string[] } | null>(null)
  const [invalidLink, setInvalidLink] = useState(!token)
  const [busy, setBusy] = useState(false)
  const errorRef = useRef<HTMLDivElement>(null)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next = { password: passwordError(password, t), repeat: repeat === password ? null : t.reset.mismatch }
    setErrors(next)
    if (next.password || next.repeat) {
      return
    }
    setBusy(true)
    setServer(null)
    try {
      await registrationApi.resetPassword(token, password)
      navigate('/login?reset=ok', { replace: true })
    } catch (error) {
      if (hasApiKey(error, RESET_TOKEN_INVALID_KEY)) {
        setInvalidLink(true)
        return
      }
      const messages = error instanceof ApiError ? error.messages : []
      setServer({
        title: messages.length > 1 ? t.reset.failed : apiErrorMessage(error, t.common.error),
        messages: messages.length > 1 ? messages : [],
      })
      window.setTimeout(() => errorRef.current?.focus(), 0)
    } finally {
      setBusy(false)
    }
  }

  if (invalidLink) {
    return (
      <SoloLayout>
        <AuthCard icon={<Link2Off aria-hidden />} tone="warn" title={t.reset.invalidTitle}>
          <p className="m-0 text-sm text-fg2">{t.reset.invalidBody}</p>
          <Link className="btn pri blk" to="/forgot-password">
            {t.reset.newLink}
          </Link>
          <Link className="lnk text-[13px]" to="/login">
            {t.forgot.backToLogin}
          </Link>
        </AuthCard>
      </SoloLayout>
    )
  }

  return (
    <AuthLayout>
      <AuthLead title={t.reset.title} />
      <form className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors ref={errorRef} title={server.title} messages={server.messages} /> : null}
        <Field label={t.reset.newPassword} error={errors.password} hint={<PasswordPolicy value={password} />}>
          <PasswordInput
            autoComplete="new-password"
            value={password}
            maxLength={TEXT_LIMITS.password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </Field>
        <Field label={t.reset.repeat} error={errors.repeat}>
          <PasswordInput
            autoComplete="new-password"
            value={repeat}
            maxLength={TEXT_LIMITS.password}
            onChange={(event) => setRepeat(event.target.value)}
          />
        </Field>
        <div className="callout text-fg2">
          <KeyRound aria-hidden />
          <span>{t.reset.sessionsNote}</span>
        </div>
        <Button type="submit" variant="primary" size="lg" block loading={busy}>
          {t.reset.submit}
        </Button>
      </form>
    </AuthLayout>
  )
}
