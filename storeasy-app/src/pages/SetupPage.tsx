import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Check, Plug } from 'lucide-react'
import { EmailVerificationBanner } from '@/components/account/EmailVerificationBanner'
import { AddConnectionDialog } from '@/components/connections/AddConnectionDialog'
import { AuthFooter } from '@/components/layout/AuthLayout'
import { Brand } from '@/components/layout/Logo'
import { CreateOrganizationForm } from '@/components/org/CreateOrganizationForm'
import { useDeployment } from '@/features/access/publicSettings'
import { Callout } from '@/components/ui/Feedback'
import { Avatar, Button } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { displayName, initialsOf, useAuth } from '@/lib/auth/AuthContext'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { HOME_PATH } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { cn } from '@/lib/utils/cn'

type StepKey = 'organization' | 'connect' | 'apps'

/**
 * First setup for a user without an organization (design 5.29-5.31): create the organization, connect a store, map the
 * apps. Every step can be skipped; progress is read from the data, not stored.
 */
export function SetupPage() {
  const t = useT()
  const navigate = useNavigate()
  const { user } = useAuth()
  const { current, switchTo, can } = useOrganization()
  const [connecting, setConnecting] = useState(false)
  const [added, setAdded] = useState(false)
  // Single team: the organization is "the team" (one per instance).
  const { singleTeam } = useDeployment()
  const step: StepKey = current ? 'connect' : 'organization'

  const steps: Array<{ key: StepKey; title: string; detail: string }> = [
    {
      key: 'organization',
      title: singleTeam ? t.setup.teamStep1 : t.setup.step1,
      detail: current ? `${current.Name} · ${current.Slug}` : t.setup.step1Detail,
    },
    { key: 'connect', title: t.setup.step2, detail: t.setup.step2Detail },
    { key: 'apps', title: t.setup.step3, detail: t.setup.step3Detail },
  ]
  const index = steps.findIndex((item) => item.key === step)

  return (
    <div className="flex min-h-full flex-col bg-bg">
      <header className="flex h-16 flex-none items-center gap-3 border-b border-border px-4 sm:px-7">
        <Brand className="!p-0" />
        <span className="text-[13px] text-fg3 max-sm:hidden">{t.setup.label}</span>
        {user ? (
          <span className="ml-auto flex items-center gap-2 text-[13px]">
            <span className="max-sm:hidden">{displayName(user)}</span>
            <Avatar initials={initialsOf(displayName(user))} />
          </span>
        ) : null}
      </header>
      {/* A new account lands here first; creating the organization waits for the e-mail verification. */}
      <EmailVerificationBanner />
      <div className="grid flex-1 content-start gap-6 px-4 py-6 sm:gap-10 sm:px-12 sm:py-9 lg:grid-cols-[260px_minmax(0,1fr)]">
        <ol className="m-0 grid list-none grid-cols-3 gap-1 self-start p-0 lg:grid-cols-1" aria-label={t.setup.label}>
          {steps.map((item, position) => (
            <li
              key={item.key}
              aria-current={item.key === step ? 'step' : undefined}
              className={cn(
                'grid items-start gap-1.5 rounded-md p-2 text-center lg:grid-cols-[28px_minmax(0,1fr)] lg:gap-3 lg:p-3 lg:text-left',
                item.key === step && 'bg-bg2',
              )}
            >
              <span
                className={cn(
                  'mono mx-auto grid h-7 w-7 place-items-center rounded-full border-[1.5px] text-xs lg:mx-0',
                  position < index
                    ? 'border-fg bg-fg text-bg'
                    : item.key === step
                      ? 'border-accent text-accent-text'
                      : 'border-borderStrong text-fg3',
                )}
              >
                {position < index ? <Check className="h-3.5 w-3.5" aria-hidden /> : position + 1}
              </span>
              <span className="min-w-0">
                <b className="block text-xs sm:text-sm">{item.title}</b>
                <small className="block text-xs text-fg3 max-lg:hidden">{item.detail}</small>
              </span>
            </li>
          ))}
        </ol>
        <main className="grid max-w-2xl content-start gap-4">
          <span className="eb">{interpolate(t.setup.stepOf, { step: index + 1, total: steps.length })}</span>
          {step === 'organization' ? (
            <>
              <h1 className="pg-t">{singleTeam ? t.setup.teamTitle : t.setup.organizationTitle}</h1>
              <p className="m-0 text-sm text-fg2">{singleTeam ? t.setup.teamSubtitle : t.organization.newSubtitle}</p>
              <div className="card p-5">
                <CreateOrganizationForm submitLabel={t.setup.createAndContinue} onCreated={(organizationId) => switchTo(organizationId)} />
              </div>
            </>
          ) : (
            <>
              <h1 className="pg-t">{t.setup.connectTitle}</h1>
              <p className="m-0 text-sm text-fg2">{t.setup.connectBody}</p>
              {can(OrgPermission.ManageConnections) ? (
                <div className="card grid gap-3 p-5">
                  <Plug className="h-6 w-6 text-fg2" aria-hidden />
                  <p className="m-0 text-sm text-fg2">{t.setup.connectHint}</p>
                  <Button variant="primary" className="justify-self-start" onClick={() => setConnecting(true)}>
                    {t.connections.add}
                  </Button>
                </div>
              ) : (
                <Callout tone="info">{t.sections.apps.askAdmin}</Callout>
              )}
              <Callout>{t.connections.encryptedNote}</Callout>
              <div className="flex flex-wrap items-center gap-2 border-t border-border pt-4">
                <Link className="btn ghost" to={HOME_PATH}>
                  {t.setup.later}
                </Link>
                <span className="text-xs text-fg3">{t.setup.laterHint}</span>
              </div>
              {current ? (
                <AddConnectionDialog
                  open={connecting}
                  organizationId={current.Id}
                  onAdded={() => setAdded(true)}
                  onClose={() => {
                    setConnecting(false)
                    if (added) {
                      navigate('/connections')
                    }
                  }}
                />
              ) : null}
            </>
          )}
        </main>
      </div>
      <div className="px-4 pb-5 sm:px-7">
        <AuthFooter />
      </div>
    </div>
  )
}
