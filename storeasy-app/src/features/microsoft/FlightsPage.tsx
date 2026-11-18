import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plane, Plus } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input, PageHeader } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { useP2 } from '@/features/common/i18n/useP2'
import { permissionLabel } from '@/features/common/labels'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { msKeys } from './keys'
import { MsWorkspace, useMsPlan, type MsContext } from './MsWorkspace'
import { MsChangeKind, type MsFlightDto, type MsFlightGroupDto, type MsFlightInputDto } from './types'

// Design 8.20-8.21: package flights by rank with their groups, creating a flight (name, at least one group, the rank it
// takes) through a plan, and the names of the Partner Center flight groups, which the API knows only by id.

/** Every group the flights name, once, with the recorded name when there is one. */
export function groupsOf(flights: MsFlightDto[]): MsFlightGroupDto[] {
  const groups = new Map<string, MsFlightGroupDto>()
  for (const flight of flights) {
    for (const id of flight.GroupIds ?? []) {
      groups.set(id, groups.get(id) ?? { Id: id })
    }
    for (const group of flight.Groups ?? []) {
      groups.set(group.Id, { ...groups.get(group.Id), ...group })
    }
  }
  return [...groups.values()]
}

function CreateFlightDialog({
  open,
  onClose,
  onPlan,
  flights,
  groups,
}: {
  open: boolean
  onClose: () => void
  onPlan: (flight: MsFlightInputDto) => void
  flights: MsFlightDto[]
  groups: MsFlightGroupDto[]
}) {
  const t = useMs()
  const [name, setName] = useState('')
  const [picked, setPicked] = useState<string[]>([])
  const [extra, setExtra] = useState('')
  const [rank, setRank] = useState<string>('')
  const [errors, setErrors] = useState<{ name?: string | null; groups?: string | null }>({})

  const close = () => {
    setName('')
    setPicked([])
    setExtra('')
    setRank('')
    setErrors({})
    onClose()
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const trimmed = name.trim()
    const taken = flights.some((flight) => (flight.FriendlyName ?? '').localeCompare(trimmed, 'tr', { sensitivity: 'accent' }) === 0)
    const next = {
      name: !trimmed ? t.flights.nameRequired : taken ? t.flights.nameTaken : null,
      groups: picked.length === 0 ? t.flights.groupsRequired : null,
    }
    setErrors(next)
    if (next.name || next.groups) {
      return
    }
    onPlan({ FriendlyName: trimmed, GroupIds: picked, RankHigherThan: rank || null })
    close()
  }

  const known = [
    ...groups,
    ...picked.filter((id) => !groups.some((group) => group.Id === id)).map((id) => ({ Id: id }) as MsFlightGroupDto),
  ]
  return (
    <Modal
      open={open}
      onClose={close}
      title={t.flights.createTitle}
      description={t.flights.createLead}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {t.submit.close}
          </Button>
          <Button type="submit" form="ms-create-flight" variant="primary">
            {t.planMake}
          </Button>
        </>
      }
    >
      <form id="ms-create-flight" className="grid gap-4" noValidate onSubmit={submit}>
        <Field label={t.flights.friendlyName} error={errors.name}>
          <Input value={name} onChange={(event) => setName(event.target.value)} />
        </Field>
        <fieldset className="grid gap-2">
          <legend className="lbl mb-1">{t.flights.pickGroups}</legend>
          {known.map((group) => (
            <Checkbox
              key={group.Id}
              checked={picked.includes(group.Id)}
              onChange={(checked) => setPicked((list) => (checked ? [...list, group.Id] : list.filter((id) => id !== group.Id)))}
            >
              {group.Name ?? `${t.flights.unnamed} · ${group.Id}`}
              {group.MemberCount ? ` · ${interpolate(t.flights.members, { count: group.MemberCount })}` : ''}
            </Checkbox>
          ))}
          <div className="flex items-end gap-2">
            <span className="flex-1">
              <Field label={t.flights.groupId}>
                <Input mono value={extra} onChange={(event) => setExtra(event.target.value)} />
              </Field>
            </span>
            <Button
              disabled={!extra.trim()}
              onClick={() => {
                const id = extra.trim()
                setPicked((list) => (list.includes(id) ? list : [...list, id]))
                setExtra('')
              }}
            >
              {t.flights.addGroupId}
            </Button>
          </div>
          {errors.groups ? <span className="err-t">{errors.groups}</span> : null}
        </fieldset>
        <Field label={t.flights.rankAbove} hint={t.flights.rankNote}>
          <Select
            aria-label={t.flights.rankAbove}
            value={rank}
            options={[
              { value: '', label: t.flights.rankLowest },
              ...flights.map((flight) => ({
                value: flight.FlightId,
                label: interpolate(t.flights.rankAboveOf, { name: flight.FriendlyName ?? flight.FlightId }),
              })),
            ]}
            onChange={setRank}
          />
        </Field>
      </form>
    </Modal>
  )
}

function GroupRow({ group, context, allowed }: { group: MsFlightGroupDto; context: MsContext; allowed: boolean }) {
  const t = useMs()
  const queryClient = useQueryClient()
  const { organizationId, record } = context
  const [name, setName] = useState(group.Name ?? '')
  const [members, setMembers] = useState(group.MemberCount ? String(group.MemberCount) : '')
  const [error, setError] = useState<string | null>(null)
  const save = useMutation({
    mutationFn: () => msApi.saveFlightGroup(organizationId, group.Id, name.trim(), members.trim() ? Number(members) : null),
    onSuccess: () => {
      showToast(t.flights.groupSaved, 'success', name.trim())
      void queryClient.invalidateQueries({ queryKey: msKeys.flights(organizationId, record.StoreAppId) })
    },
  })
  return (
    <li className="grid gap-2 rounded-md border border-border p-3 sm:grid-cols-[1fr_120px_auto] sm:items-end">
      <Field label={`${t.flights.groupName} · ${group.Id}`} error={error}>
        <Input value={name} disabled={!allowed} onChange={(event) => setName(event.target.value)} />
      </Field>
      <Field label={t.flights.memberCount}>
        <Input
          inputMode="numeric"
          value={members}
          disabled={!allowed}
          onChange={(event) => setMembers(event.target.value.replace(/\D/g, ''))}
        />
      </Field>
      {allowed ? (
        <Button
          loading={save.isPending}
          aria-label={`${t.flights.saveGroup}: ${group.Id}`}
          onClick={() => {
            const problem = name.trim() ? null : t.flights.groupNameRequired
            setError(problem)
            if (!problem) {
              save.mutate()
            }
          }}
        >
          {t.flights.saveGroup}
        </Button>
      ) : null}
    </li>
  )
}

function Flights({ context }: { context: MsContext }) {
  const t = useMs()
  const p2 = useP2()
  const { organizationId, record, editable, editor, can, app } = context
  const [creating, setCreating] = useState(false)
  const plan = useMsPlan(context)
  const flights = useQuery({
    queryKey: msKeys.flights(organizationId, record.StoreAppId),
    queryFn: () => msApi.flights(organizationId, record.StoreAppId),
  })
  if (flights.isLoading) {
    return <CardSkeleton />
  }
  if (flights.isError) {
    return <ScreenState error={flights.error} onRetry={() => void flights.refetch()} />
  }
  const list = [...(flights.data ?? [])].sort((a, b) => (a.Rank ?? 999) - (b.Rank ?? 999))
  const groups = groupsOf(list)
  const allowed = can(OrgPermission.ReleaseTest)
  return (
    <>
      <PageHeader
        title={t.flights.title}
        subtitle={t.flights.lead}
        actions={
          editor ? (
            <Button variant="primary" disabled={!allowed || !editable} loading={plan.pending} onClick={() => setCreating(true)}>
              <Plus aria-hidden />
              {t.flights.create}
            </Button>
          ) : undefined
        }
      />
      {plan.element}
      {editor && !allowed ? (
        <p className="mb-3 text-xs text-fg3">
          {interpolate(t.flights.needs, { permission: permissionLabel(OrgPermission.ReleaseTest, p2) })}
        </p>
      ) : null}
      {list.length === 0 ? (
        <EmptyState icon={<Plane aria-hidden />} title={t.flights.none} />
      ) : (
        <table className="tbl mb-4">
          <thead>
            <tr>
              <th>{t.flights.rank}</th>
              <th>{t.flights.name}</th>
              <th>{t.flights.groups}</th>
              <th>{t.flights.published}</th>
              <th>{t.flights.pending}</th>
            </tr>
          </thead>
          <tbody>
            {list.map((flight) => (
              <tr key={flight.FlightId}>
                <td className="mono">{flight.Rank ?? '-'}</td>
                <td>
                  <b>{flight.FriendlyName ?? flight.FlightId}</b>
                </td>
                <td>
                  <span className="flex flex-wrap gap-1">
                    {(flight.GroupIds ?? []).map((id) => {
                      const group = groups.find((item) => item.Id === id)
                      return (
                        <Badge key={id} small>
                          {group?.Name ?? id}
                        </Badge>
                      )
                    })}
                  </span>
                </td>
                <td className="mono">{flight.LastPublishedSubmissionId ?? '-'}</td>
                <td className="mono">{flight.PendingSubmissionId ?? '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <section className="card" aria-label={t.flights.groupsTitle}>
        <div className="card-h">
          <h2 className="h3 flex-1">{t.flights.groupsTitle}</h2>
          <Badge small tone="warning">
            {t.addons.assisted}
          </Badge>
        </div>
        <div className="card-b grid gap-3">
          <p className="m-0 text-[13px] text-fg2">{t.flights.groupsLead}</p>
          <ul className="m-0 grid list-none gap-2 p-0">
            {groups.map((group) => (
              <GroupRow key={group.Id} group={group} context={context} allowed={editor && allowed} />
            ))}
          </ul>
          <Callout tone="info">
            {t.flights.assistedRank}{' '}
            <Link className="lnk" to={`/apps/${app.Id}/assisted?store=microsoft`}>
              {t.plan.openAssisted}
            </Link>
          </Callout>
        </div>
      </section>
      <CreateFlightDialog
        open={creating}
        onClose={() => setCreating(false)}
        onPlan={(flight) => plan.make({ Kind: MsChangeKind.CreateFlight, Flight: flight })}
        flights={list}
        groups={groups}
      />
    </>
  )
}

export function MsFlightsPage() {
  return <MsWorkspace sub="flights">{(context) => <Flights context={context} />}</MsWorkspace>
}
