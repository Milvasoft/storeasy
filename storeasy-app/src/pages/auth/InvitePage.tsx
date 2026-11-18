import { useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Link2Off, MailWarning, UserPlus } from 'lucide-react'
import { AuthCard, SoloLayout } from '@/components/layout/AuthLayout'
import { FormErrors } from '@/components/ui/Feedback'
import { Avatar, Button } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, apiStatus, hasApiKey } from '@/lib/api/http'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useT } from '@/lib/i18n/I18nContext'
import { withPlaceholders } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

export const INVITATION_EMAIL_MISMATCH_KEY = 'InvitationEmailMismatch'

type Outcome = { kind: 'mismatch'; message: string } | { kind: 'invalid'; message: string } | { kind: 'error'; message: string } | null

/**
 * Accepting an invitation (design 5.22-5.25). The invitation preview endpoint (organization, inviter, role, status) is not
 * on the API yet, so the page speaks of "an organization" until the accept call answers; the accept itself is real.
 */
export function InvitePage() {
  const t = useT()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { token: pathToken } = useParams()
  const [params] = useSearchParams()
  const token = pathToken ?? params.get('token') ?? ''
  const { status, user, logout } = useAuth()
  const { switchTo } = useOrganization()
  const [busy, setBusy] = useState(false)
  const [outcome, setOutcome] = useState<Outcome>(null)
  const returnUrl = `/invite/${encodeURIComponent(token)}`

  const accept = async () => {
    setBusy(true)
    setOutcome(null)
    try {
      const organizationId = await organizationsApi.acceptInvitation(token)
      await queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all })
      switchTo(organizationId)
      showToast(t.invite.joined, 'success')
      navigate(HOME_PATH, { replace: true })
    } catch (error) {
      if (apiStatus(error) === 409) {
        // Already a member: go straight to the organization (5.23).
        await queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all })
        navigate(HOME_PATH, { replace: true })
      } else if (hasApiKey(error, INVITATION_EMAIL_MISMATCH_KEY)) {
        setOutcome({ kind: 'mismatch', message: apiErrorMessage(error, t.invite.mismatchBody) })
      } else if (apiStatus(error) === 400 || apiStatus(error) === 404) {
        setOutcome({ kind: 'invalid', message: apiErrorMessage(error, t.invite.invalidBody) })
      } else {
        setOutcome({ kind: 'error', message: apiErrorMessage(error, t.common.error) })
      }
    } finally {
      setBusy(false)
    }
  }

  const switchAccount = async () => {
    await logout()
    navigate(`/login?returnUrl=${encodeURIComponent(returnUrl)}`, { replace: true })
  }

  if (!token || outcome?.kind === 'invalid') {
    return (
      <SoloLayout>
        <AuthCard icon={<Link2Off aria-hidden />} tone="warn" title={t.invite.invalidTitle}>
          <p className="m-0 text-sm text-fg2">{outcome?.message ?? t.invite.invalidBody}</p>
          <Link className="btn inv blk" to={status === 'authed' ? HOME_PATH : '/login'}>
            {status === 'authed' ? t.invite.backToApp : t.invite.toLogin}
          </Link>
        </AuthCard>
      </SoloLayout>
    )
  }

  if (outcome?.kind === 'mismatch' && user) {
    return (
      <SoloLayout>
        <AuthCard icon={<MailWarning aria-hidden />} tone="warn" title={t.invite.mismatchTitle}>
          <p className="m-0 text-sm text-fg2">{outcome.message}</p>
          <p className="m-0 text-[13px] text-fg3">
            {withPlaceholders(t.invite.signedInAs, { email: <b className="text-fg">{user.email}</b> })}
          </p>
          <div className="flex flex-wrap justify-center gap-2">
            <Link className="btn ghost" to={HOME_PATH}>
              {t.invite.backToApp}
            </Link>
            <Button variant="inverse" onClick={() => void switchAccount()}>
              {t.invite.switchAccount}
            </Button>
          </div>
        </AuthCard>
      </SoloLayout>
    )
  }

  return (
    <SoloLayout>
      <AuthCard icon={<UserPlus aria-hidden />} title={t.invite.title}>
        <p className="m-0 text-sm text-fg2">{t.invite.body}</p>
        <ul className="m-0 grid list-none gap-1.5 rounded-md border border-border bg-bg2/50 p-4 text-left text-[13px] text-fg2">
          {t.invite.points.map((point) => (
            <li key={point}>{point}</li>
          ))}
        </ul>
        {outcome?.kind === 'error' ? <FormErrors title={outcome.message} /> : null}
        {status === 'authed' && user ? (
          <>
            <div className="flex items-center gap-2.5 rounded-md border border-border p-3 text-left">
              <Avatar initials={initialsOf(displayName(user))} />
              <span className="min-w-0 text-[13px]">
                <span className="block text-fg3">{t.invite.signedInLabel}</span>
                <b className="block truncate">{user.email}</b>
              </span>
            </div>
            <div className="flex flex-wrap justify-center gap-2">
              <Link className="btn ghost" to={HOME_PATH}>
                {t.invite.notNow}
              </Link>
              <Button variant="primary" loading={busy} onClick={() => void accept()}>
                {t.invite.accept}
              </Button>
            </div>
          </>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2">
            <Link className="btn" to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`}>
              {t.invite.loginAndJoin}
            </Link>
            <Link className="btn pri" to={`/register?invite=${encodeURIComponent(token)}`}>
              {t.invite.registerAndJoin}
            </Link>
          </div>
        )}
      </AuthCard>
    </SoloLayout>
  )
}
