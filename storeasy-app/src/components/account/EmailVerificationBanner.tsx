import { useEffect, useState } from 'react'
import { Send } from 'lucide-react'
import { Banner } from '@/components/ui/Feedback'
import { Button } from '@/components/ui/primitives'
import { registrationApi } from '@/lib/api/endpoints'
import { apiErrorMessage, apiStatus, type ApiError } from '@/lib/api/http'
import { useAuth } from '@/lib/auth/AuthContext'
import { formatCountdown, useCountdown } from '@/lib/hooks/useCountdown'
import { useT } from '@/lib/i18n/I18nContext'
import { withPlaceholders } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'

const RESEND_SECONDS = 60

/**
 * The strip above the content while the signed-in user's e-mail is not verified (design 5.7 wording): organizations and
 * store connections wait for it. "Send the link again" counts down 60 s, or the API's Retry-After after a 429. Coming
 * back to the tab reads the account again, so a link opened elsewhere clears the strip.
 */
export function EmailVerificationBanner() {
  const t = useT()
  const { user, needsVerification, refreshUser } = useAuth()
  const countdown = useCountdown()
  const [busy, setBusy] = useState(false)
  const [sent, setSent] = useState(false)

  useEffect(() => {
    if (!needsVerification) {
      return
    }
    const onFocus = () => void refreshUser()
    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [needsVerification, refreshUser])

  if (!needsVerification || !user) {
    return null
  }

  const resend = async () => {
    setBusy(true)
    try {
      await registrationApi.resendVerification()
      setSent(true)
      countdown.start(RESEND_SECONDS)
    } catch (error) {
      if (apiStatus(error) === 409) {
        // EmailAlreadyVerified: the link was opened meanwhile.
        showToast(t.emailVerify.alreadyVerified, 'success')
        await refreshUser()
      } else {
        countdown.start((error as ApiError).retryAfterSeconds ?? RESEND_SECONDS)
        showToast(apiErrorMessage(error, t.common.error), 'error')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <Banner
      tone="warning"
      role="status"
      action={
        <Button size="sm" variant="inverse" loading={busy} disabled={countdown.seconds > 0} onClick={() => void resend()}>
          <Send aria-hidden />
          {countdown.seconds > 0 ? `${t.emailVerify.resend} · ${formatCountdown(countdown.seconds)}` : t.emailVerify.resend}
        </Button>
      }
    >
      <b>{sent ? t.emailVerify.resent : t.emailVerify.bannerTitle}</b>{' '}
      <span className="text-fg2">
        {withPlaceholders(t.emailVerify.bannerBody, { email: <b className="text-fg">{user.email ?? user.userName}</b> })}
      </span>
    </Banner>
  )
}
