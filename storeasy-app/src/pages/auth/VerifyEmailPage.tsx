import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom'
import { CircleCheck, Clock, MailCheck } from 'lucide-react'
import { AuthCard, SoloLayout } from '@/components/layout/AuthLayout'
import { LogoMark } from '@/components/layout/Logo'
import { FormErrors } from '@/components/ui/Feedback'
import { Button } from '@/components/ui/primitives'
import { registrationApi } from '@/lib/api/endpoints'
import { apiStatus, type ApiError } from '@/lib/api/http'
import { useAuth } from '@/lib/auth/AuthContext'
import { formatCountdown, useCountdown } from '@/lib/hooks/useCountdown'
import { useT } from '@/lib/i18n/I18nContext'
import { withPlaceholders } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'

const RESEND_SECONDS = 60

/**
 * "Send again" for the signed-in user (the API sends to the account's own address; a signed-out visitor is asked to sign
 * in first). 60 s countdown, or the API's Retry-After after a 429; 409 means the address was verified meanwhile.
 */
function ResendButton({ label, onVerified }: { label: string; onVerified: () => void }) {
  const t = useT()
  const countdown = useCountdown()
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)

  const resend = async () => {
    setBusy(true)
    try {
      await registrationApi.resendVerification()
      setSent(true)
      countdown.start(RESEND_SECONDS)
    } catch (error) {
      if (apiStatus(error) === 409) {
        onVerified()
      } else {
        countdown.start((error as ApiError).retryAfterSeconds ?? RESEND_SECONDS)
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid gap-3">
      {sent ? <FormErrors tone="info" title={t.verify.resent} /> : null}
      <Button variant="primary" block loading={busy} disabled={countdown.seconds > 0} onClick={() => void resend()}>
        {countdown.seconds > 0 ? `${label} · ${formatCountdown(countdown.seconds)}` : label}
      </Button>
    </div>
  )
}

function SignInToResend() {
  const t = useT()
  return (
    <>
      <p className="m-0 text-[13px] text-fg3">{t.verify.signedOutNote}</p>
      <Link className="btn inv blk" to="/login">
        {t.verify.signInToResend}
      </Link>
    </>
  )
}

function DoneCard() {
  const t = useT()
  const { status } = useAuth()
  const authed = status === 'authed'
  return (
    <AuthCard icon={<CircleCheck aria-hidden />} tone="ok" title={t.verify.doneTitle}>
      <p className="m-0 text-sm text-fg2">{authed ? t.verify.doneBodySignedIn : t.verify.doneBody}</p>
      {authed ? (
        <Link className="btn pri lg blk" to={HOME_PATH}>
          {t.common.continue}
        </Link>
      ) : (
        <Link className="btn pri lg blk" to="/login?verified=1">
          {t.login.submit}
        </Link>
      )}
    </AuthCard>
  )
}

/**
 * "Check your e-mail" after sign-up (design 5.14): the link is valid for 24 hours, resend counts down 60 s. The e-mail's
 * link is /verify-email?token=... (AppOptions.VerifyEmailPath), so a token in the query verifies instead.
 */
export function VerifyEmailPage() {
  const t = useT()
  const location = useLocation()
  const [params] = useSearchParams()
  const { status, user, refreshUser } = useAuth()
  const email = (location.state as { email?: string } | null)?.email ?? user?.email ?? null

  if (params.get('token')) {
    return <VerifyEmailTokenPage />
  }

  if (status === 'authed' && user?.emailConfirmed === true) {
    return (
      <SoloLayout>
        <DoneCard />
      </SoloLayout>
    )
  }

  return (
    <SoloLayout>
      <AuthCard icon={<MailCheck aria-hidden />} title={t.verify.checkTitle}>
        {email ? (
          <p className="m-0 text-sm text-fg2">{withPlaceholders(t.verify.checkBody, { email: <b className="text-fg">{email}</b> })}</p>
        ) : null}
        <p className="m-0 text-[13px] text-fg2">{t.verify.notArrived}</p>
        {status === 'authed' ? (
          <>
            <ResendButton label={t.verify.resend} onVerified={() => void refreshUser()} />
            <Link className="btn ghost blk" to={HOME_PATH}>
              {t.common.continue}
            </Link>
          </>
        ) : status === 'guest' ? (
          <SignInToResend />
        ) : null}
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

type TokenState = { kind: 'pending' } | { kind: 'done' } | { kind: 'invalid' }

/**
 * The link from the e-mail (design 5.15, 5.16): verifies on open, signed in or not (verifying does not sign in). The API
 * answers one AccountTokenInvalid for an expired, used or replaced link; a new one is sent to the signed-in account.
 */
export function VerifyEmailTokenPage() {
  const t = useT()
  const { token: pathToken } = useParams()
  const [params] = useSearchParams()
  const token = pathToken ?? params.get('token') ?? ''
  const { status, refreshUser } = useAuth()
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
      .catch(() => setState({ kind: 'invalid' }))
  }, [token])

  // A signed-in tab reads its account again, so the verification banner goes away at once.
  useEffect(() => {
    if (state.kind === 'done' && status === 'authed') {
      void refreshUser()
    }
  }, [state.kind, status, refreshUser])

  return (
    <SoloLayout>
      {state.kind === 'pending' ? (
        <div className="card grid justify-items-center gap-3 p-8 text-center" role="status">
          <LogoMark size={48} spinning />
          <span className="text-sm text-fg2">{t.verify.checking}</span>
        </div>
      ) : state.kind === 'done' ? (
        <DoneCard />
      ) : (
        <AuthCard icon={<Clock aria-hidden />} tone="warn" title={t.verify.expiredTitle}>
          <p className="m-0 text-sm text-fg2">{t.verify.expiredBody}</p>
          {status === 'authed' ? (
            <ResendButton label={t.verify.newLink} onVerified={() => setState({ kind: 'done' })} />
          ) : (
            <SignInToResend />
          )}
        </AuthCard>
      )}
    </SoloLayout>
  )
}
