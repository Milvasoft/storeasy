import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Callout } from '@/components/ui/Feedback'
import { Button, Checkbox, Field, Input, PageHeader, RadioGroup } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { showToast } from '@/lib/toast/toast'
import { appContentApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import type { P2Dictionary } from '@/features/common/i18n/en'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { splitList } from '@/features/common/text'
import type { AppProfileInput } from '@/features/common/types'

// The app's catalog profile (UpdateAppProfileCommand): what the rules need to know that the stores do not report. The
// values are the catalog's own vocabulary (rules/*.json appliesWhen), so a rule matches exactly.

export const PLATFORMS = [
  'ios',
  'ipados',
  'macos',
  'tvos',
  'visionos',
  'watchos',
  'android',
  'wear-os',
  'android-tv',
  'android-automotive',
  'android-xr',
]
export const DEVICE_FAMILIES = [
  'iphone',
  'ipad',
  'mac',
  'apple-tv',
  'apple-vision',
  'apple-watch',
  'phone',
  'tablet-7',
  'tablet-10',
  'chromebook',
  'desktop',
  'xbox',
  'holographic',
]
export const FEATURES = [
  'subscriptions',
  'uses-location',
  'shared-location',
  'dating-relationships',
  'aigc',
  'e2ee-messaging',
  'anonymous-chat',
  'random-chat',
  'on-demand-resources',
  'uses-contacts',
  'uses-read-call-log',
  'foreground-service',
  'uses-appfunctions',
  'uses-local-network',
]

const REGION = /^[A-Z]{2}$/

/** Invalid region codes of a comma separated list (empty when every code is two letters). */
export function invalidRegions(value: string): string[] {
  return splitList(value.toUpperCase()).filter((code) => !REGION.test(code))
}

type LiveChoice = 'auto' | 'yes' | 'no'

function ChoiceGroup({
  legend,
  options,
  selected,
  onToggle,
  disabled,
}: {
  legend: string
  options: string[]
  selected: string[]
  onToggle: (value: string) => void
  disabled: boolean
}) {
  return (
    <fieldset className="grid gap-2">
      <legend className="lbl mb-1 text-[13px] font-semibold">{legend}</legend>
      <div className="flex flex-wrap gap-x-4 gap-y-2">
        {options.map((option) => (
          <Checkbox key={option} checked={selected.includes(option)} onChange={() => onToggle(option)} disabled={disabled}>
            <span className="mono">{option}</span>
          </Checkbox>
        ))}
      </div>
    </fieldset>
  )
}

function toggle(list: string[], value: string): string[] {
  return list.includes(value) ? list.filter((item) => item !== value) : [...list, value]
}

function listOrNull(list: string[]): string[] | null {
  return list.length > 0 ? list : null
}

function ProfileForm({ context, t }: { context: WorkspaceContext; t: P2Dictionary }) {
  const queryClient = useQueryClient()
  const { app, organizationId, editor } = context
  const [platforms, setPlatforms] = useState<string[]>([])
  const [families, setFamilies] = useState<string[]>([])
  const [features, setFeatures] = useState<string[]>([])
  const [regions, setRegions] = useState('')
  const [developerRegion, setDeveloperRegion] = useState('')
  const [appType, setAppType] = useState<'unknown' | 'app' | 'game'>('unknown')
  const [buildSdk, setBuildSdk] = useState('')
  const [targetSdk, setTargetSdk] = useState('')
  const [live, setLive] = useState<LiveChoice>('auto')
  const [errors, setErrors] = useState<{ regions?: string; developerRegion?: string; targetSdk?: string }>({})

  const save = useMutation({
    mutationFn: (profile: AppProfileInput) => appContentApi.updateProfile(organizationId, app.Id, profile),
    onSuccess: () => {
      showToast(t.profile.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: contentKeys.all })
    },
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const badRegions = invalidRegions(regions)
    const badDeveloper = developerRegion.trim() ? invalidRegions(developerRegion) : []
    const target = targetSdk.trim() ? Number(targetSdk) : null
    const next = {
      regions: badRegions.length > 0 ? interpolate(t.profile.regionsInvalid, { codes: badRegions.join(', ') }) : undefined,
      developerRegion: badDeveloper.length > 0 ? interpolate(t.profile.regionsInvalid, { codes: badDeveloper.join(', ') }) : undefined,
      targetSdk: target !== null && (!Number.isInteger(target) || target < 1 || target > 99) ? t.profile.targetSdkInvalid : undefined,
    }
    setErrors(next)
    if (next.regions || next.developerRegion || next.targetSdk) {
      return
    }
    save.mutate({
      Platforms: listOrNull(platforms),
      DeviceFamilies: listOrNull(families),
      Features: listOrNull(features),
      Regions: listOrNull(splitList(regions.toUpperCase())),
      DeveloperRegion: developerRegion.trim().toUpperCase() || null,
      AppType: appType === 'unknown' ? null : appType,
      BuildSdkVersion: buildSdk.trim() || null,
      TargetSdk: target,
      IsLive: live === 'auto' ? null : live === 'yes',
    })
  }

  const disabled = !editor
  return (
    <form className="grid max-w-3xl gap-5" onSubmit={submit} noValidate>
      <Callout tone="info">{t.profile.unknownNote}</Callout>
      <ChoiceGroup
        legend={t.profile.platforms}
        options={PLATFORMS}
        selected={platforms}
        onToggle={(value) => setPlatforms(toggle(platforms, value))}
        disabled={disabled}
      />
      <ChoiceGroup
        legend={t.profile.deviceFamilies}
        options={DEVICE_FAMILIES}
        selected={families}
        onToggle={(value) => setFamilies(toggle(families, value))}
        disabled={disabled}
      />
      <ChoiceGroup
        legend={t.profile.features}
        options={FEATURES}
        selected={features}
        onToggle={(value) => setFeatures(toggle(features, value))}
        disabled={disabled}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t.profile.regions} hint={t.profile.regionsHint} error={errors.regions} optional>
          <Input value={regions} onChange={(event) => setRegions(event.target.value)} disabled={disabled} mono />
        </Field>
        <Field label={t.profile.developerRegion} error={errors.developerRegion} optional>
          <Input
            value={developerRegion}
            maxLength={2}
            onChange={(event) => setDeveloperRegion(event.target.value)}
            disabled={disabled}
            mono
          />
        </Field>
        <Field label={t.profile.buildSdk} hint={t.profile.buildSdkHint} optional>
          <Input value={buildSdk} onChange={(event) => setBuildSdk(event.target.value)} disabled={disabled} mono />
        </Field>
        <Field label={t.profile.targetSdk} error={errors.targetSdk} optional>
          <Input value={targetSdk} inputMode="numeric" onChange={(event) => setTargetSdk(event.target.value)} disabled={disabled} mono />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <RadioGroup
          name="app-type"
          label={t.profile.appType}
          value={appType}
          onChange={(value) => setAppType(value)}
          options={[
            { value: 'unknown', label: t.profile.unknown, disabled },
            { value: 'app', label: t.profile.appTypeApp, disabled },
            { value: 'game', label: t.profile.appTypeGame, disabled },
          ]}
        />
        <RadioGroup
          name="is-live"
          label={t.profile.isLive}
          value={live}
          onChange={(value) => setLive(value)}
          options={[
            { value: 'auto', label: t.profile.isLiveAuto, disabled },
            { value: 'yes', label: t.profile.isLiveYes, disabled },
            { value: 'no', label: t.profile.isLiveNo, disabled },
          ]}
        />
      </div>
      {editor ? (
        <div>
          <Button type="submit" variant="primary" loading={save.isPending}>
            {t.profile.save}
          </Button>
        </div>
      ) : null}
    </form>
  )
}

function ProfileScreen({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  return (
    <>
      <PageHeader title={t.profile.title} subtitle={t.profile.subtitle} />
      <ProfileForm context={context} t={t} />
    </>
  )
}

export function AppProfilePage() {
  return (
    <AppWorkspace section="profile" appWide>
      {(context) => <ProfileScreen context={context} />}
    </AppWorkspace>
  )
}
