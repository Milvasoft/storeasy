import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Badge, StoreTag } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, Input, Switch } from '@/components/ui/primitives'
import { OrgPermission, type StoreKind, type AppListDto } from '@/lib/api/types'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { msApi } from '@/features/microsoft/api'
import { msKeys } from '@/features/microsoft/keys'
import { testFlightApi, testFlightKeys } from '@/features/testflight/api'
import { intakeApi, intakeKeys } from './api'
import { useIntakeText } from './i18n'
import { IntakeShell, useIntakeApps } from './IntakeShell'
import { PLATFORMS, platformLabel, storeOf, targetOf } from './labels'
import { ArtifactPlatform, DeliveryTarget, DeliveryWarningAction, PLAY_TEST_TRACKS, PRODUCTION_TRACKS, type DeliveryRuleDto } from './types'

// Design 13.18: one rule per app and platform, test channels only (TestFlight internal group, Play test track with an
// optional staged rollout, Microsoft package flight). The server refuses production, external groups and submission.

const CUSTOM_TRACK = '__custom'

function defaultRule(platform: ArtifactPlatform): DeliveryRuleDto {
  return {
    Platform: platform,
    Enabled: false,
    Target: targetOf(platform),
    TargetId: platform === ArtifactPlatform.Android ? 'internal' : null,
    TargetName: platform === ArtifactPlatform.Android ? 'internal' : null,
    OnWarning: DeliveryWarningAction.Hold,
    RolloutFraction: null,
  }
}

function storeAppIdOf(app: AppListDto, store: StoreKind): number | null {
  return app.Stores?.find((record) => record.Store === store)?.StoreAppId ?? null
}

function RuleEditor({
  app,
  rule,
  disabled,
  onChange,
}: {
  app: AppListDto
  rule: DeliveryRuleDto
  disabled: boolean
  onChange: (rule: DeliveryRuleDto) => void
}) {
  const t = useIntakeText()
  const organization = useCurrentOrganization()
  const storeAppId = storeAppIdOf(app, storeOf(rule.Platform))
  const groups = useQuery({
    queryKey: testFlightKeys.groups(organization.Id, storeAppId ?? 0),
    queryFn: () => testFlightApi.groups(organization.Id, storeAppId ?? 0),
    enabled: rule.Platform === ArtifactPlatform.Ios && storeAppId !== null,
  })
  const flights = useQuery({
    queryKey: msKeys.flights(organization.Id, storeAppId ?? 0),
    queryFn: () => msApi.flights(organization.Id, storeAppId ?? 0),
    enabled: rule.Platform === ArtifactPlatform.Windows && storeAppId !== null,
  })
  const [customTrack, setCustomTrack] = useState(
    rule.Platform === ArtifactPlatform.Android && !!rule.TargetId && !PLAY_TEST_TRACKS.includes(rule.TargetId),
  )
  const label = platformLabel(rule.Platform, t)
  const set = (changes: Partial<DeliveryRuleDto>) => onChange({ ...rule, ...changes })
  const setTarget = (id: string | null, name?: string | null) =>
    set({ Target: id ? targetOf(rule.Platform) : DeliveryTarget.None, TargetId: id, TargetName: name ?? id })
  const production = !!rule.TargetId && PRODUCTION_TRACKS.includes(rule.TargetId.trim().toLowerCase())
  const internalGroups = (groups.data ?? []).filter((group) => group.IsInternal)

  return (
    <li className="card grid gap-3 p-4">
      <span className="flex flex-wrap items-center justify-between gap-2">
        <span className="flex items-center gap-2">
          <StoreTag store={storeOf(rule.Platform)} small codeOnly />
          <b className="text-[14px]">{label}</b>
        </span>
        <span className="flex items-center gap-2 text-[13px]">
          <Switch
            checked={rule.Enabled}
            disabled={disabled || storeAppId === null}
            onChange={(enabled) => set({ Enabled: enabled })}
            label={interpolate(t.rules.enabled, { platform: label })}
          />
        </span>
      </span>
      {storeAppId === null ? (
        <span className="text-[13px] text-fg3">{interpolate(t.rules.noStore, { platform: label })}</span>
      ) : rule.Platform === ArtifactPlatform.Extension ? (
        <span className="text-[13px] text-fg2">{t.rules.extensionUpload}</span>
      ) : rule.Platform === ArtifactPlatform.Ios ? (
        <Field label={t.rules.group} hint={t.rules.groupHint}>
          {internalGroups.length > 0 ? (
            <Select<string>
              value={rule.Target === DeliveryTarget.None ? '' : (rule.TargetId ?? '')}
              disabled={disabled}
              options={[
                { value: '', label: t.rules.targetNone },
                ...internalGroups.map((group) => ({ value: group.Name ?? String(group.Id), label: group.Name ?? `#${group.Id}` })),
              ]}
              onChange={(value) => setTarget(value || null)}
            />
          ) : (
            <Input
              value={rule.TargetId ?? ''}
              placeholder={t.rules.groupName}
              disabled={disabled}
              maxLength={200}
              onChange={(event) => setTarget(event.target.value || null)}
            />
          )}
        </Field>
      ) : rule.Platform === ArtifactPlatform.Android ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t.rules.track} error={production ? t.rules.productionRefused : null}>
            <Select<string>
              value={customTrack ? CUSTOM_TRACK : rule.Target === DeliveryTarget.None ? '' : (rule.TargetId ?? '')}
              disabled={disabled}
              options={[
                { value: '', label: t.rules.targetNone },
                ...PLAY_TEST_TRACKS.map((track) => ({ value: track, label: track })),
                { value: CUSTOM_TRACK, label: t.rules.trackCustomOption },
              ]}
              onChange={(value) => {
                setCustomTrack(value === CUSTOM_TRACK)
                setTarget(value === CUSTOM_TRACK ? null : value || null)
              }}
            />
          </Field>
          {customTrack ? (
            <Field label={t.rules.trackCustom}>
              <Input
                value={rule.TargetId ?? ''}
                disabled={disabled}
                maxLength={200}
                onChange={(event) => setTarget(event.target.value || null)}
              />
            </Field>
          ) : null}
          <Field label={t.rules.rollout} hint={t.rules.rolloutHint} optional>
            <Input
              type="number"
              min={1}
              max={100}
              value={rule.RolloutFraction ? String(Math.round(rule.RolloutFraction * 100)) : ''}
              disabled={disabled}
              onChange={(event) => {
                const percent = Number(event.target.value)
                set({ RolloutFraction: event.target.value && percent > 0 && percent < 100 ? percent / 100 : null })
              }}
            />
          </Field>
        </div>
      ) : (
        <Field label={t.rules.flight}>
          {(flights.data ?? []).length > 0 ? (
            <Select<string>
              value={rule.Target === DeliveryTarget.None ? '' : (rule.TargetId ?? '')}
              disabled={disabled}
              options={[
                { value: '', label: t.rules.targetNone },
                ...(flights.data ?? []).map((flight) => ({ value: flight.FlightId, label: flight.FriendlyName ?? flight.FlightId })),
              ]}
              onChange={(value) => {
                const flight = (flights.data ?? []).find((item) => item.FlightId === value)
                setTarget(value || null, flight?.FriendlyName ?? value)
              }}
            />
          ) : (
            <Input
              value={rule.TargetId ?? ''}
              placeholder={t.rules.flightId}
              disabled={disabled}
              maxLength={200}
              onChange={(event) => setTarget(event.target.value || null)}
            />
          )}
        </Field>
      )}
      {storeAppId !== null ? (
        <span className="grid gap-0.5">
          <Checkbox
            checked={rule.OnWarning === DeliveryWarningAction.Hold}
            disabled={disabled}
            onChange={(hold) => set({ OnWarning: hold ? DeliveryWarningAction.Hold : DeliveryWarningAction.Deliver })}
          >
            {t.rules.onWarning}
          </Checkbox>
          <span className="pl-6 text-xs text-fg3">{t.rules.onWarningHint}</span>
        </span>
      ) : null}
    </li>
  )
}

function AppRules({ app }: { app: AppListDto }) {
  const t = useIntakeText()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const queryClient = useQueryClient()
  const manage = can(OrgPermission.ManageConnections)
  const rules = useQuery({ queryKey: intakeKeys.rules(organization.Id, app.Id), queryFn: () => intakeApi.rules(organization.Id, app.Id) })
  const [draft, setDraft] = useState<DeliveryRuleDto[] | null>(null)
  useEffect(() => {
    if (rules.data) {
      setDraft(PLATFORMS.map((platform) => rules.data.Rules?.find((rule) => rule.Platform === platform) ?? defaultRule(platform)))
    }
  }, [rules.data])
  const save = useMutation({
    mutationFn: (list: DeliveryRuleDto[]) => intakeApi.saveRules(organization.Id, app.Id, list),
    onSuccess: () => {
      showToast(t.rules.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
    },
  })

  if (rules.isLoading || (!draft && !rules.isError)) {
    return <CardSkeleton />
  }
  if (rules.isError || !draft) {
    return <ScreenState error={rules.error} onRetry={() => void rules.refetch()} />
  }
  const stored = new Set((app.Stores ?? []).map((record) => record.Store))
  // Only the platforms the app has a store for are sent; a switched off one keeps its row (Enabled false).
  // A rule without a target goes as None: the server accepts a target kind only with its id (IntakeRules.IsTargetAllowed).
  const outgoing = draft
    .filter((rule) => stored.has(storeOf(rule.Platform)))
    .map((rule) =>
      rule.Target !== DeliveryTarget.None && !rule.TargetId?.trim()
        ? { ...rule, Target: DeliveryTarget.None, TargetId: null, TargetName: null, RolloutFraction: null }
        : rule,
    )
  const invalid = outgoing.some((rule) => !!rule.TargetId && PRODUCTION_TRACKS.includes(rule.TargetId.trim().toLowerCase()))
  return (
    <div className="grid gap-3">
      <span className="flex flex-wrap items-center justify-between gap-2">
        <span className="grid">
          <b className="text-[15px]">{app.Name}</b>
          {rules.data?.UpdatedByUserName ? (
            <span className="text-xs text-fg3">
              {interpolate(t.rules.updated, { name: rules.data.UpdatedByUserName, date: format.date(rules.data.UpdatedDate) })}
            </span>
          ) : null}
        </span>
        {manage ? (
          <Button variant="primary" loading={save.isPending} disabled={invalid} onClick={() => save.mutate(outgoing)}>
            {t.rules.save}
          </Button>
        ) : (
          <span className="text-xs text-fg3">{t.manageOnly}</span>
        )}
      </span>
      <ul className="grid gap-3">
        {draft.map((rule, index) => (
          <RuleEditor
            key={rule.Platform}
            app={app}
            rule={rule}
            disabled={!manage}
            onChange={(next) => setDraft(draft.map((item, position) => (position === index ? next : item)))}
          />
        ))}
      </ul>
      <Callout tone="warning" title={t.rules.neverTitle}>
        {t.rules.neverBody}
      </Callout>
    </div>
  )
}

export function IntakeRulesPage() {
  const t = useIntakeText()
  const organization = useCurrentOrganization()
  const apps = useIntakeApps()
  const summary = useQuery({ queryKey: intakeKeys.summary(organization.Id), queryFn: () => intakeApi.summary(organization.Id) })
  const [params, setParams] = useSearchParams()
  const list = apps.data ?? []
  const selectedId = Number(params.get('app')) || list[0]?.Id || null
  const selected = list.find((app) => app.Id === selectedId) ?? null
  const ruleCount = (appId: number) =>
    (summary.data?.find((row) => row.AppId === appId)?.Rules ?? []).filter((rule) => rule.Enabled && rule.Target !== DeliveryTarget.None)
      .length

  return (
    <IntakeShell>
      {apps.isLoading ? (
        <CardSkeleton />
      ) : apps.isError ? (
        <ScreenState error={apps.error} onRetry={() => void apps.refetch()} />
      ) : list.length === 0 ? (
        <EmptyState title={t.rules.noApps} />
      ) : (
        <div className="grid gap-4 md:grid-cols-[220px_minmax(0,1fr)]">
          <nav aria-label={t.rules.caption} className="grid content-start gap-1">
            {list.map((app) => {
              const count = ruleCount(app.Id)
              return (
                <button
                  key={app.Id}
                  type="button"
                  aria-current={app.Id === selectedId ? 'page' : undefined}
                  className={`flex items-center justify-between gap-2 rounded-md px-3 py-2 text-left text-[13px] ${app.Id === selectedId ? 'bg-bg2 font-semibold' : ''}`}
                  onClick={() => setParams({ app: String(app.Id) }, { replace: true })}
                >
                  <span className="truncate">{app.Name}</span>
                  <Badge small tone={count > 0 ? 'accent' : 'neutral'}>
                    {count > 0 ? interpolate(t.rules.count, { count }) : t.rules.none}
                  </Badge>
                </button>
              )
            })}
          </nav>
          {selected ? <AppRules key={selected.Id} app={selected} /> : <p className="text-[13px] text-fg3">{t.rules.pickApp}</p>}
        </div>
      )}
    </IntakeShell>
  )
}
