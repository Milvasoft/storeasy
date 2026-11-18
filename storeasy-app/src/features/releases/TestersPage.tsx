import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Chip, Field, IconButton, Input, PageHeader } from '@/components/ui/primitives'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { interpolate } from '@/lib/i18n/interpolate'
import { releasesApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { PlanDialog } from '@/features/common/PlanView'
import type { ChangePlanDto } from '@/features/common/types'

// Play testers per track (Google Groups); a change is a plan that needs ReleaseTest.

const DEFAULT_TRACKS = ['internal', 'alpha', 'beta']
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function Testers({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const { organizationId, record, can } = context
  const allowed = can(OrgPermission.ReleaseTest)
  const overview = useQuery({
    queryKey: contentKeys.releases(organizationId, record.StoreAppId),
    queryFn: () => releasesApi.overview(organizationId, record.StoreAppId),
  })
  const tracks = (overview.data?.Tracks ?? []).map((track) => track.Track).filter((track) => track.toLowerCase() !== 'production')
  const choices = tracks.length > 0 ? tracks : DEFAULT_TRACKS
  const [track, setTrack] = useState<string | null>(null)
  const current = track ?? choices[0]
  const testers = useQuery({
    queryKey: contentKeys.testers(organizationId, record.StoreAppId, current),
    queryFn: () => releasesApi.testers(organizationId, record.StoreAppId, current),
    enabled: !overview.isLoading,
  })
  const [groups, setGroups] = useState<Record<string, string[]>>({})
  const [adding, setAdding] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const list = groups[current] ?? testers.data?.GoogleGroups ?? []
  const changed = current in groups
  const makePlan = useMutation({
    mutationFn: () => releasesApi.planTesters(organizationId, record.StoreAppId, current, list),
    onSuccess: setPlan,
  })

  if (overview.isLoading || testers.isLoading) {
    return <CardSkeleton />
  }
  if (testers.isError) {
    return <ScreenState error={testers.error} onRetry={() => void testers.refetch()} />
  }

  const add = () => {
    const email = adding.trim().toLowerCase()
    if (!EMAIL.test(email)) {
      setError(t.testers.invalid)
      return
    }
    setGroups((previous) => ({ ...previous, [current]: [...new Set([...list, email])] }))
    setAdding('')
    setError(null)
  }

  return (
    <div className="grid max-w-2xl gap-4">
      <PageHeader title={t.testers.title} subtitle={t.testers.subtitle} />
      <div className="toolbar" role="group" aria-label={t.testers.track}>
        {choices.map((choice) => (
          <Chip key={choice} selected={choice === current} onClick={() => setTrack(choice)}>
            <span className="mono">{choice}</span>
          </Chip>
        ))}
      </div>
      <section className="card" aria-label={t.testers.groups}>
        <div className="card-h">
          <h2 className="h3">{t.testers.groups}</h2>
        </div>
        <div className="card-b grid gap-3">
          {list.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.testers.empty}</p> : null}
          <ul className="m-0 grid list-none gap-1 p-0">
            {list.map((email) => (
              <li key={email} className="flex items-center gap-2 text-[13px]">
                <span className="mono flex-1">{email}</span>
                {allowed ? (
                  <IconButton
                    label={interpolate(t.testers.remove, { email })}
                    onClick={() => setGroups((value) => ({ ...value, [current]: list.filter((item) => item !== email) }))}
                  >
                    <X aria-hidden />
                  </IconButton>
                ) : null}
              </li>
            ))}
          </ul>
          {allowed ? (
            <div className="flex items-end gap-2">
              <div className="flex-1">
                <Field label={t.testers.addLabel} error={error}>
                  <Input
                    type="email"
                    value={adding}
                    onChange={(event) => setAdding(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter') {
                        event.preventDefault()
                        add()
                      }
                    }}
                  />
                </Field>
              </div>
              <Button onClick={add}>{t.testers.add}</Button>
            </div>
          ) : (
            <p className="m-0 text-xs text-fg3">{t.release.needsTest}</p>
          )}
        </div>
      </section>
      {allowed ? (
        <div>
          <Button variant="primary" disabled={!changed} loading={makePlan.isPending} onClick={() => makePlan.mutate()}>
            {t.testers.plan}
          </Button>
        </div>
      ) : null}
      <PlanDialog plan={plan} onClose={() => setPlan(null)} onDecided={() => setGroups({})} />
    </div>
  )
}

export function TestersPage() {
  return (
    <AppWorkspace section="release" sub="testers" stores={[StoreKind.Google]}>
      {(context) => <Testers context={context} />}
    </AppWorkspace>
  )
}
