import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom'
import { CircleCheck, Clock, MailCheck } from 'lucide-react'
import { AuthCard, SoloLayout } from '@/components/layout/AuthLayout'
import { LogoMark } from '@/components/layout/Logo'
import { FormErrors } from '@/components/ui/Feedback'
import { Button, Field, Input } from '@/components/ui/primitives'
import { registrationApi } from '@/lib/api/endpoints'
import { type ApiError, apiErrorMessage, hasApiKey } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { formatCountdown, useCountdown } from '@/lib/hooks/useCountdown'
import { useT } from '@/lib/i18n/I18nContext'
import { withPlaceholders } from '@/lib/i18n/interpolate'
import { emailError } from '@/lib/validation'

export const VERIFICATION_USED_KEY = 'VerificationTokenUsed'
const RESEND_SECONDS = 60

// A resend form: the answer is the same whether or not the address has an account; 429 starts the countdown.
function ResendForm({ initialEmail, submitLabel }: { initialEmail: string; submitLabel: string }) {
  const t = useT()
  const [email, setEmail] = useState(initialEmail)
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)
  const countdown = useCountdown()

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const problem = emailError(email, t)
    setError(problem)
    if (problem) {
      return
    }
    setBusy(true)
    try {
      await registrationApi.resendVerification(email.trim())
      setSent(true)
      countdown.start(RESEND_SECONDS)
    } catch (failure) {
      countdown.start((failure as ApiError).retryAfterSeconds ?? RESEND_SECONDS)
      setError(apiErrorMessage(failure, t.common.error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="grid gap-3 text-left" onSubmit={submit} noValidate>
      {sent ? <FormErrors tone="info" title={t.verify.resent} /> : null}
      <Field label={t.fields.email} error={error}>
        <Input
          type="email"
          autoComplete="email"
          value={email}
          maxLength={TEXT_LIMITS.email}
          onChange={(event) => setEmail(event.target.value)}
        />
      </Field>
      <Button type="submit" variant="primary" block loading={busy} disabled={countdown.seconds > 0}>
        {countdown.seconds > 0 ? `${submitLabel} · ${formatCountdown(countdown.seconds)}` : submitLabel}
      </Button>
    </form>
  )
}

/** "Check your e-mail" after sign-up (design 5.14): the link is valid for 24 hours, resend counts down 60 s. */
export function VerifyEmailPage() {
  const t = useT()
  const location = useLocation()
  const [params] = useSearchParams()
  const email = (location.state as { email?: string } | null)?.email ?? params.get('email') ?? ''
  const countdown = useCountdown()
  const [sent, setSent] = useState(false)

  const resend = async () => {
    try {
      await registrationApi.resendVerification(email)
      setSent(true)
      countdown.start(RESEND_SECONDS)
    } catch (error) {
      countdown.start((error as ApiError).retryAfterSeconds ?? RESEND_SECONDS)
    }
  }

  return (
    <SoloLayout>
      <AuthCard icon={<MailCheck aria-hidden />} title={t.verify.checkTitle}>
        <p className="m-0 text-sm text-fg2">
          {email ? withPlaceholders(t.verify.checkBody, { email: <b className="text-fg">{email}</b> }) : t.verify.checkBodyNoEmail}
        </p>
        {sent ? <FormErrors tone="info" title={t.verify.resent} /> : null}
        {email ? (
          <p className="m-0 text-[13px] text-fg2">
            {t.verify.notArrived}{' '}
            <button type="button" className="lnk" disabled={countdown.seconds > 0} onClick={() => void resend()}>
              {countdown.seconds > 0 ? `${t.verify.resend} · ${formatCountdown(countdown.seconds)}` : t.verify.resend}
            </button>
          </p>
        ) : (
          <ResendForm initialEmail="" submitLabel={t.verify.resend} />
        )}
        <p className="m-0 text-[13px] text-fg3">
          {t.verify.wrongAddress}{' '}
          <Link className="lnk" to="/register">
            {t.verify.changeAddress}
          </Link>
        </p>
      </AuthCard>
    </SoloLayout>
  )
}

type TokenState = { kind: 'pending' } | { kind: 'done' } | { kind: 'used' } | { kind: 'expired'; message: string }

/** The link from the e-mail (design 5.15, 5.16): verifies on open; success leads to sign-in (verification does not sign in). */
export function VerifyEmailTokenPage() {
  const t = useT()
  const { token = '' } = useParams()
  const [state, setState] = useState<TokenState>({ kind: 'pending' })
  const started = useRef(false)

  useEffect(() => {
    // StrictMode mounts twice in development; the token is single use, so it is sent once.
    if (started.current) {
      return
    }
    started.current = true
    registrationApi
      .verifyEmail(token)
      .then(() => setState({ kind: 'done' }))
      .catch((error) =>
        setState(
          hasApiKey(error, VERIFICATION_USED_KEY)
            ? { kind: 'used' }
            : { kind: 'expired', message: apiErrorMessage(error, t.verify.expiredBody) },
        ),
      )
  }, [token, t.verify.expiredBody])

  return (
    <SoloLayout>
      {state.kind === 'pending' ? (
        <div className="card grid justify-items-center gap-3 p-8 text-center" role="status">
          <LogoMark size={48} spinning />
          <span className="text-sm text-fg2">{t.verify.checking}</span>
        </div>
      ) : state.kind === 'done' ? (
        <AuthCard icon={<CircleCheck aria-hidden />} tone="ok" title={t.verify.doneTitle}>
          <p className="m-0 text-sm text-fg2">{t.verify.doneBody}</p>
          <Link className="btn pri lg blk" to="/login?verified=1">
            {t.login.submit}
          </Link>
        </AuthCard>
      ) : state.kind === 'used' ? (
        <AuthCard icon={<CircleCheck aria-hidden />} title={t.verify.usedTitle}>
          <p className="m-0 text-sm text-fg2">{t.verify.usedBody}</p>
          <Link className="btn inv blk" to="/login">
            {t.login.submit}
          </Link>
        </AuthCard>
      ) : (
        <AuthCard icon={<Clock aria-hidden />} tone="warn" title={t.verify.expiredTitle}>
          <p className="m-0 text-sm text-fg2">{state.message}</p>
          <ResendForm initialEmail="" submitLabel={t.verify.newLink} />
        </AuthCard>
      )}
    </SoloLayout>
  )
}
