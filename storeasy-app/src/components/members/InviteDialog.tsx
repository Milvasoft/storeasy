import { useRef, useState, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { SecretBox } from '@/components/ui/Code'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, Field, RadioGroup, Textarea } from '@/components/ui/primitives'
import { organizationsApi } from '@/lib/api/endpoints'
import { type ApiError, apiErrorMessage, apiStatus } from '@/lib/api/http'
import { OrgRole, type InvitationCreatedDto } from '@/lib/api/types'
import { formatCountdown, useCountdown } from '@/lib/hooks/useCountdown'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { canAssign, roleDescription, roleLabel } from '@/lib/org/roles'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { emailError } from '@/lib/validation'

const MAX_ADDRESSES = 10

export function inviteLink(token: string): string {
  return `${window.location.origin}/invite/${encodeURIComponent(token)}`
}

function splitAddresses(value: string): string[] {
  return Array.from(
    new Set(
      value
        .split(/[\s,;]+/)
        .map((part) => part.trim())
        .filter(Boolean),
    ),
  )
}

interface Created {
  email: string
  invitation: InvitationCreatedDto
}

/**
 * Inviting people (design 15.5-15.8): one request per address (the API takes one e-mail), the link of each invitation
 * shown once because e-mails are not sent yet, 409 per address keeps that address in the form, 429 counts down.
 */
export function InviteDialog({
  open,
  onClose,
  organizationId,
  actorRole,
}: {
  open: boolean
  onClose: () => void
  organizationId: number
  actorRole: OrgRole
}) {
  const t = useT()
  const format = useFormat()
  const queryClient = useQueryClient()
  const [addresses, setAddresses] = useState('')
  const [role, setRole] = useState<OrgRole>(OrgRole.Editor)
  const [error, setError] = useState<string | null>(null)
  const [failures, setFailures] = useState<string[]>([])
  const [created, setCreated] = useState<Created[]>([])
  const [busy, setBusy] = useState(false)
  const countdown = useCountdown()
  const errorRef = useRef<HTMLDivElement>(null)

  const close = () => {
    setAddresses('')
    setRole(OrgRole.Editor)
    setError(null)
    setFailures([])
    setCreated([])
    onClose()
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const list = splitAddresses(addresses)
    const invalid = list.find((address) => emailError(address, t))
    const problem =
      list.length === 0
        ? t.validation.emailRequired
        : list.length > MAX_ADDRESSES
          ? interpolate(t.members.tooMany, { count: MAX_ADDRESSES })
          : invalid
            ? interpolate(t.members.invalidAddress, { email: invalid })
            : null
    setError(problem)
    if (problem) {
      return
    }
    setBusy(true)
    setFailures([])
    const done: Created[] = []
    const failed: string[] = []
    const left: string[] = []
    for (const email of list) {
      try {
        done.push({ email, invitation: await organizationsApi.invite(organizationId, email, role) })
      } catch (failure) {
        if (apiStatus(failure) === 429) {
          countdown.start((failure as ApiError).retryAfterSeconds ?? 60)
          failed.push(apiErrorMessage(failure, t.members.throttled))
          left.push(...list.slice(list.indexOf(email)))
          break
        }
        failed.push(`${email}: ${apiErrorMessage(failure, t.common.error)}`)
        left.push(email)
      }
    }
    setBusy(false)
    setCreated((current) => [...current, ...done])
    setFailures(failed)
    // The addresses that did not go stay in the form (design 15.7).
    setAddresses(left.join(', '))
    if (done.length > 0) {
      void queryClient.invalidateQueries({ queryKey: queryKeys.members.all })
      showToast(interpolate(t.members.invited, { count: done.length }), 'success')
    }
    if (failed.length > 0) {
      window.setTimeout(() => errorRef.current?.focus(), 0)
    }
  }

  const roles = [OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer, OrgRole.Owner]

  return (
    <Modal
      open={open}
      onClose={close}
      title={t.members.inviteTitle}
      description={t.members.inviteSubtitle}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {created.length > 0 ? t.common.done : t.common.cancel}
          </Button>
          <Button type="submit" form="invite-form" variant="primary" loading={busy} disabled={countdown.seconds > 0}>
            {countdown.seconds > 0 ? `${t.members.sendInvite} · ${formatCountdown(countdown.seconds)}` : t.members.sendInvite}
          </Button>
        </>
      }
    >
      <form id="invite-form" className="grid gap-4" onSubmit={(event) => void submit(event)} noValidate>
        {failures.length > 0 ? (
          <FormErrors
            ref={errorRef}
            title={t.members.someFailed}
            messages={failures.length > 1 ? failures : []}
            detail={failures.length === 1 ? failures[0] : undefined}
          />
        ) : null}
        {created.length > 0 ? (
          <div className="grid gap-3">
            <Callout tone="info" title={t.members.linkOnce}>
              {t.members.linkOnceBody}
            </Callout>
            {created.map((item) => (
              <div key={item.invitation.Id} className="grid gap-1.5">
                <span className="text-[13px]">
                  <b>{item.email}</b>{' '}
                  <span className="text-fg3">· {interpolate(t.members.validUntil, { date: format.date(item.invitation.ExpiryDate) })}</span>
                </span>
                <SecretBox value={inviteLink(item.invitation.Token)} />
              </div>
            ))}
          </div>
        ) : null}
        <Field label={t.members.emails} error={error} hint={t.members.emailsHint}>
          <Textarea
            rows={2}
            value={addresses}
            autoComplete="off"
            spellCheck={false}
            onChange={(event) => setAddresses(event.target.value)}
            className="!min-h-[64px]"
          />
        </Field>
        <fieldset className="grid gap-1 border-0 p-0">
          <legend className="lbl mb-1">{t.members.role}</legend>
          <RadioGroup
            name="invite-role"
            label={t.members.role}
            value={role}
            onChange={setRole}
            options={roles.map((value) => ({
              value,
              label: roleLabel(value, t),
              description: value === OrgRole.Owner ? t.members.ownerViaTransfer : roleDescription(value, t),
              // The validator refuses Owner (OrganizationRoleInvalid: ownership comes by transfer); the rest follow CanAssign.
              disabled: value === OrgRole.Owner || !canAssign(actorRole, null, value),
            }))}
          />
        </fieldset>
        <p className="m-0 text-xs text-fg3">{t.members.reinviteNote}</p>
      </form>
    </Modal>
  )
}
