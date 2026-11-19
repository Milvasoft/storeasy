import { useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Clock, Link2Off, MailWarning, UserPlus } from 'lucide-react'
import { AuthCard, SoloLayout } from '@/components/layout/AuthLayout'
import { OrgAvatar } from '@/components/org/OrgAvatar'
import { FormErrors } from '@/components/ui/Feedback'
import { Avatar, Button, SpinnerCenter } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, apiStatus, hasApiKey } from '@/lib/api/http'
import { InvitationStatus, type InvitationPreviewDto } from '@/lib/api/types'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate, withPlaceholders } from '@/lib/i18n/interpolate'
import { roleLabel } from '@/lib/org/roles'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { useAccessText } from '@/features/access/i18n'
import { permissionName } from '@/features/access/roles'

export const INVITATION_EMAIL_MISMATCH_KEY = 'InvitationEmailMismatch'

type Outcome = { kind: 'mismatch'; message: string } | { kind: 'invalid'; message: string } | { kind: 'error'; message: string } | null

/** What the invitation offers, before anything is accepted: organization, role and its permissions, inviter, expiry. */
function PreviewDetails({ preview }: { preview: InvitationPreviewDto }) {
  const t = useT()
  const access = useAccessText()
  const format = useFormat()
  const role = preview.Role ? roleLabel(preview.Role, t, preview.CustomRoleName) : null
  return (
    <div className="grid gap-3 rounded-md border border-border bg-bg2/50 p-4 text-left text-[13px]">
      <dl className="kv">
        <dt>{access.invitePreview.organization}</dt>
        <dd className="flex min-w-0 items-center gap-2">
          {preview.OrganizationName ? (
            <OrgAvatar name={preview.OrganizationName} logoUrl={preview.OrganizationLogoUrl} className="!h-6 !w-6 !rounded-md !text-[11px]" />
          ) : null}
          <span className="min-w-0">
            <b>{preview.OrganizationName}</b>
            {preview.OrganizationSlug ? <span className="mono ml-1.5 text-xs text-fg3">{preview.OrganizationSlug}</span> : null}
          </span>
        </dd>
        {role ? (
          <>
            <dt>{access.invitePreview.role}</dt>
            <dd>{role}</dd>
          </>
        ) : null}
        {preview.InviterName ? (
          <>
            <dt>{access.invitePreview.inviter}</dt>
            <dd>{preview.InviterName}</dd>
          </>
        ) : null}
        {preview.MaskedEmail ? (
          <>
            <dt>{access.invitePreview.sentTo}</dt>
            <dd className="mono">{preview.MaskedEmail}</dd>
          </>
        ) : null}
        {preview.ExpiryDate ? (
          <>
            <dt>{access.invitePreview.expires}</dt>
            <dd>{format.dateTime(preview.ExpiryDate)}</dd>
          </>
        ) : null}
      </dl>
      {(preview.Permissions ?? []).length > 0 ? (
        <div className="grid gap-1">
          <b>{access.invitePreview.permissions}</b>
          <ul className="m-0 grid list-none gap-1 p-0 text-fg2" aria-label={access.invitePreview.permissions}>
            {(preview.Permissions ?? []).map((permission) => (
              <li key={permission}>{access.roles.permission[permissionName(permission)]}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  )
}

/**
 * Accepting an invitation (design 5.22-5.25). The anonymous preview (organizations/invitation/preview) shows the
 * organization, role, inviter, masked address and expiry before anything is accepted; an expired, revoked or used
 * invitation gets its own card. The accept call stays the authority.
 */
export function InvitePage() {
  const t = useT()
  const access = useAccessText()
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
  const preview = useQuery({
    queryKey: ['invitation-preview', token],
    queryFn: () => organizationsApi.invitationPreview(token),
    enabled: !!token,
    retry: false,
  })

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

  if (token && preview.isLoading) {
    return (
      <SoloLayout>
        <SpinnerCenter label={access.invitePreview.loading} />
      </SoloLayout>
    )
  }

  if (!token || outcome?.kind === 'invalid' || apiStatus(preview.error) === 404) {
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

  const details = preview.data
  if (details && details.Status !== InvitationStatus.Pending) {
    const names = { organization: details.OrganizationName ?? '', inviter: details.InviterName || access.invitePreview.someone }
    const view =
      details.Status === InvitationStatus.Expired
        ? {
            icon: <Clock aria-hidden />,
            title: access.invitePreview.expiredTitle,
            body: interpolate(access.invitePreview.expiredBody, names),
          }
        : details.Status === InvitationStatus.Revoked
          ? {
              icon: <Link2Off aria-hidden />,
              title: access.invitePreview.revokedTitle,
              body: interpolate(access.invitePreview.revokedBody, names),
            }
          : {
              icon: <UserPlus aria-hidden />,
              title: access.invitePreview.acceptedTitle,
              body: interpolate(access.invitePreview.acceptedBody, names),
            }
    return (
      <SoloLayout>
        <AuthCard icon={view.icon} tone="warn" title={view.title}>
          <p className="m-0 text-sm text-fg2">{view.body}</p>
          <Link className="btn inv blk" to={status === 'authed' ? HOME_PATH : '/login'}>
            {status === 'authed' ? t.invite.backToApp : t.invite.toLogin}
          </Link>
        </AuthCard>
      </SoloLayout>
    )
  }

  return (
    <SoloLayout>
      <AuthCard
        icon={<UserPlus aria-hidden />}
        title={
          details?.OrganizationName ? interpolate(access.invitePreview.title, { organization: details.OrganizationName }) : t.invite.title
        }
      >
        {details ? (
          <PreviewDetails preview={details} />
        ) : (
          <>
            <p className="m-0 text-sm text-fg2">{t.invite.body}</p>
            <ul className="m-0 grid list-none gap-1.5 rounded-md border border-border bg-bg2/50 p-4 text-left text-[13px] text-fg2">
              {t.invite.points.map((point) => (
                <li key={point}>{point}</li>
              ))}
            </ul>
          </>
        )}
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
