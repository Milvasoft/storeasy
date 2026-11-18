import { useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { safeReturnPath, type LoginState } from '@/components/common/ProtectedRoute'
import { AuthLayout, AuthLead } from '@/components/layout/AuthLayout'
import { FormErrors } from '@/components/ui/Feedback'
import { Button, Field, Input, PasswordInput } from '@/components/ui/primitives'
import { apiErrorMessage, apiStatus, type ApiError } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { useAuth } from '@/lib/auth/AuthContext'
import { formatCountdown, useCountdown } from '@/lib/hooks/useCountdown'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { emailError } from '@/lib/validation'
import { readStorage, StorageKey } from '@/lib/utils/storage'
import { GoogleSignInButton } from '@/features/access/GoogleSignIn'

// Retry-After is missing on some proxies; the API's window for sign-in is a minute.
const DEFAULT_RETRY_SECONDS = 60

type Problem = { kind: 'error'; title: string; detail?: string } | { kind: 'throttled' }

/**
 * Sign in by e-mail (design 5.1-5.7): wrong credentials, lockout text, 429 countdown, session ended. An unverified e-mail
 * does not stop the sign-in (the API lets the account in); the shell says what waits for the verification.
 */
export function LoginPage() {
  const t = useT()
  const { login, sessionEnded } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const state = location.state as LoginState | null
  const returnTo = safeReturnPath(state?.from) ?? safeReturnPath(params.get('returnUrl'))

  const [email, setEmail] = useState(() => params.get('email') ?? readStorage(StorageKey.LastEmail) ?? '')
  const [password, setPassword] = useState('')
  const [emailProblem, setEmailProblem] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const throttle = useCountdown()
  const passwordRef = useRef<HTMLInputElement>(null)
  const errorRef = useRef<HTMLDivElement>(null)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const invalid = emailError(email, t)
    setEmailProblem(invalid)
    if (invalid || !password) {
      if (!invalid) {
        passwordRef.current?.focus()
      }
      return
    }
    setBusy(true)
    setProblem(null)
    try {
      await login(email, password)
      navigate(returnTo ?? HOME_PATH, { replace: true })
    } catch (error) {
      if (apiStatus(error) === 429) {
        throttle.start((error as ApiError).retryAfterSeconds ?? DEFAULT_RETRY_SECONDS)
        setProblem({ kind: 'throttled' })
      } else {
        // The server never says which field is wrong: the box stays above the form, the password is cleared (5.2).
        setProblem({
          kind: 'error',
          title: apiErrorMessage(error, t.login.failed),
          detail: apiStatus(error) === 401 ? t.login.failedHint : undefined,
        })
        setPassword('')
        passwordRef.current?.focus()
      }
      window.setTimeout(() => errorRef.current?.focus(), 0)
    } finally {
      setBusy(false)
    }
  }

  const info =
    params.get('reset') === 'ok'
      ? { title: t.login.resetDone, detail: t.login.resetDoneDetail }
      : sessionEnded || state?.sessionEnded
        ? { title: t.login.sessionEnded, detail: returnTo ? t.login.sessionEndedReturn : undefined }
        : params.get('verified') === '1'
          ? { title: t.login.verified }
          : null

  return (
    <AuthLayout>
      <AuthLead title={t.login.title} lead={t.login.lead} />
      <form className="grid gap-4" onSubmit={submit} noValidate>
        {problem?.kind === 'error' ? <FormErrors ref={errorRef} title={problem.title} detail={problem.detail} /> : null}
        {problem?.kind === 'throttled' ? (
          <FormErrors
            ref={errorRef}
            title={t.login.throttled}
            detail={throttle.seconds > 0 ? interpolate(t.login.throttledDetail, { seconds: throttle.seconds }) : t.login.throttledDone}
          />
        ) : null}
        {info && !problem ? <FormErrors tone="info" title={info.title} detail={info.detail} /> : null}
        <Field label={t.fields.email} error={emailProblem}>
          <Input
            type="email"
            autoComplete="username"
            inputMode="email"
            value={email}
            maxLength={TEXT_LIMITS.email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </Field>
        <Field label={t.fields.password}>
          <PasswordInput
            ref={passwordRef}
            autoComplete="current-password"
            value={password}
            maxLength={TEXT_LIMITS.password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </Field>
        <div className="flex justify-end text-[12.5px]">
          <Link className="lnk" to="/forgot-password" state={{ email: email.trim() }}>
            {t.login.forgot}
          </Link>
        </div>
        <Button type="submit" variant="primary" size="lg" block loading={busy} disabled={throttle.seconds > 0}>
          {throttle.seconds > 0 ? `${t.login.submit} · ${formatCountdown(throttle.seconds)}` : t.login.submit}
        </Button>
        <GoogleSignInButton returnTo={returnTo} />
        <p className="m-0 text-center text-[12.5px] text-fg2">
          {t.login.noAccount}{' '}
          <Link className="lnk" to="/register">
            {t.login.register}
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}
