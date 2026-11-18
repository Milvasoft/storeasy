import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, RefreshCw, Rss, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { SecretBox } from '@/components/ui/Code'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { Callout } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Button, CardSkeleton, Checkbox, EmptyState, Field, RadioGroup, Textarea } from '@/components/ui/primitives'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { absoluteUrl } from '@/features/mcp/snippets'
import { CopyButton, ShownOnceDialog, Snippet } from '@/features/tokens/ShownOnce'
import { intakeApi, intakeKeys } from './api'
import { useIntakeText, type IntakeDictionary } from './i18n'
import { IntakeShell, useIntakeApps } from './IntakeShell'
import { PLATFORMS, platformLabel } from './labels'
import { EAS_DOMAINS, easCommand, isDomainEntry, parseDomains, webhookSample } from './snippets'
import {
  ArtifactPlatform,
  ArtifactPullSourceKind,
  type ArtifactSourceDto,
  type ArtifactSourceSavedDto,
  type SaveArtifactSourceInput,
} from './types'

// Design 13.19-13.20: pull sources. EAS (expo-signature) and the generic signed webhook (storeasy.artifact-ready/v1)
// call storeasy, which downloads the package from the allowed domains only. The URL and secret show once.

function kindLabel(kind: ArtifactPullSourceKind, t: IntakeDictionary): string {
  return kind === ArtifactPullSourceKind.Eas ? t.sources.eas : t.sources.webhook
}

function sourceName(source: ArtifactSourceDto, t: IntakeDictionary): string {
  return `${kindLabel(source.Kind, t)} · ${source.AppName ?? `#${source.AppId}`}`
}

function SourceDialog({
  source,
  onClose,
  onSaved,
}: {
  source: ArtifactSourceDto | null
  onClose: () => void
  onSaved: (saved: ArtifactSourceSavedDto, kind: ArtifactPullSourceKind, appId: number, rotated: boolean) => void
}) {
  const t = useIntakeText()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const apps = useIntakeApps()
  const [kind, setKind] = useState<ArtifactPullSourceKind>(source?.Kind ?? ArtifactPullSourceKind.Eas)
  const [appId, setAppId] = useState<number | null>(source?.AppId ?? null)
  const [domains, setDomains] = useState((source?.AllowedDomains ?? EAS_DOMAINS).join('\n'))
  const [platforms, setPlatforms] = useState<ArtifactPlatform[]>(source?.Platforms ?? [ArtifactPlatform.Ios, ArtifactPlatform.Android])
  const [apk, setApk] = useState(source?.IncludeApk ?? false)
  const [rotate, setRotate] = useState(false)
  const list = parseDomains(domains)
  const bad = list.find((entry) => !isDomainEntry(entry))
  const save = useMutation({
    mutationFn: () => {
      const input: SaveArtifactSourceInput = {
        Id: source?.Id ?? null,
        Kind: kind,
        AppId: appId ?? 0,
        AllowedDomains: list,
        Platforms: platforms,
        IncludeApk: apk,
        RotateSecret: rotate,
      }
      return intakeApi.saveSource(organization.Id, input)
    },
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
      onSaved(saved, kind, appId ?? 0, !!source && rotate)
    },
  })
  const ready = appId !== null && list.length > 0 && !bad && platforms.length > 0
  return (
    <Modal
      open
      onClose={onClose}
      title={source ? t.sourceForm.editTitle : t.sourceForm.title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button variant="primary" loading={save.isPending} disabled={!ready} onClick={() => save.mutate()}>
            {t.sourceForm.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        {!source ? (
          <RadioGroup<ArtifactPullSourceKind>
            name="source-kind"
            label={t.sourceForm.kind}
            value={kind}
            options={[
              { value: ArtifactPullSourceKind.Eas, label: t.sources.eas, description: t.sourceForm.easHint },
              { value: ArtifactPullSourceKind.Webhook, label: t.sources.webhook, description: t.sourceForm.webhookHint },
            ]}
            onChange={(next) => {
              setKind(next)
              if (next === ArtifactPullSourceKind.Webhook && domains === EAS_DOMAINS.join('\n')) {
                setDomains('')
              }
            }}
          />
        ) : null}
        <Field label={t.sourceForm.app}>
          <Select<number>
            value={appId}
            disabled={!!source}
            placeholder={base.common.select}
            options={(apps.data ?? []).map((app) => ({ value: app.Id, label: app.Name }))}
            onChange={setAppId}
          />
        </Field>
        <Field
          label={t.sourceForm.domains}
          hint={t.sourceForm.domainsHint}
          error={bad ? interpolate(t.sourceForm.domainsInvalid, { value: bad }) : null}
        >
          <Textarea mono rows={3} value={domains} onChange={(event) => setDomains(event.target.value)} />
        </Field>
        <fieldset className="grid gap-1.5">
          <legend className="text-[13px] font-medium">{t.sourceForm.platforms}</legend>
          {PLATFORMS.map((platform) => (
            <Checkbox
              key={platform}
              checked={platforms.includes(platform)}
              onChange={(checked) => setPlatforms(checked ? [...platforms, platform] : platforms.filter((item) => item !== platform))}
            >
              {platformLabel(platform, t)}
            </Checkbox>
          ))}
          <Checkbox checked={apk} onChange={setApk}>
            {t.sourceForm.apk}
          </Checkbox>
        </fieldset>
        {source ? (
          <Checkbox checked={rotate} onChange={setRotate}>
            {t.sourceForm.rotate}
          </Checkbox>
        ) : null}
      </div>
    </Modal>
  )
}

function SourceCard({
  source,
  manage,
  onEdit,
  onSaved,
}: {
  source: ArtifactSourceDto
  manage: boolean
  onEdit: () => void
  onSaved: (saved: ArtifactSourceSavedDto) => void
}) {
  const t = useIntakeText()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const name = sourceName(source, t)
  const url = absoluteUrl(source.Url ?? '')
  const rotate = useMutation({
    mutationFn: () =>
      intakeApi.saveSource(organization.Id, {
        Id: source.Id,
        Kind: source.Kind,
        AppId: source.AppId,
        AllowedDomains: source.AllowedDomains ?? [],
        Platforms: source.Platforms ?? [],
        IncludeApk: source.IncludeApk,
        RotateSecret: true,
      }),
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
      onSaved(saved)
    },
  })
  const remove = useMutation({
    mutationFn: () => intakeApi.deleteSource(organization.Id, source.Id),
    onSuccess: () => {
      showToast(t.sources.deleted, 'success')
      void queryClient.invalidateQueries({ queryKey: intakeKeys.all })
    },
  })
  return (
    <li className="card grid gap-2 p-4">
      <span className="flex flex-wrap items-center justify-between gap-2">
        <span className="flex items-center gap-2">
          <Badge small>{kindLabel(source.Kind, t)}</Badge>
          <b className="text-[14px]">{source.AppName ?? `#${source.AppId}`}</b>
        </span>
        <span className="text-xs text-fg3">
          {t.sources.lastCall}:{' '}
          {source.LastCallDate
            ? `${format.dateTime(source.LastCallDate)}${source.LastCallResult ? ` · ${source.LastCallResult}` : ''}`
            : t.sources.never}
        </span>
      </span>
      <span className="flex items-center gap-2">
        <code className="mono min-w-0 flex-1 break-all text-[13px]">{url}</code>
        <CopyButton value={url} label={interpolate(t.sources.copyUrl, { name })} />
      </span>
      <dl className="grid gap-1 text-[13px] sm:grid-cols-3">
        <div>
          <dt className="text-xs text-fg3">{t.sources.secret}</dt>
          <dd>{interpolate(t.sources.secretSet, { date: format.date(source.SecretSetDate) })}</dd>
        </div>
        <div>
          <dt className="text-xs text-fg3">{t.sources.domains}</dt>
          <dd className="mono break-all">{(source.AllowedDomains ?? []).join(', ')}</dd>
        </div>
        <div>
          <dt className="text-xs text-fg3">{t.sources.platforms}</dt>
          <dd>
            {(source.Platforms ?? []).map((platform) => platformLabel(platform, t)).join(', ')}
            {source.IncludeApk ? ` · ${t.sources.apk}` : ''}
          </dd>
        </div>
      </dl>
      {manage ? (
        <span className="flex flex-wrap justify-end gap-2">
          <Button size="sm" aria-label={interpolate(t.sources.editOf, { name })} onClick={onEdit}>
            <Pencil aria-hidden />
            {t.sources.edit}
          </Button>
          <Button
            size="sm"
            loading={rotate.isPending}
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.sources.rotateTitle, { name }),
                  message: t.sources.rotateBody,
                  confirmLabel: t.sources.rotate,
                })
              ) {
                rotate.mutate()
              }
            }}
          >
            <RefreshCw aria-hidden />
            {t.sources.rotate}
          </Button>
          <Button
            size="sm"
            variant="danger-outline"
            loading={remove.isPending}
            aria-label={interpolate(t.sources.deleteOf, { name })}
            onClick={async () => {
              if (
                await confirm({
                  title: interpolate(t.sources.deleteTitle, { name }),
                  message: t.sources.deleteBody,
                  danger: true,
                  confirmLabel: t.sources.delete,
                })
              ) {
                remove.mutate()
              }
            }}
          >
            <Trash2 aria-hidden />
            {t.sources.delete}
          </Button>
        </span>
      ) : null}
    </li>
  )
}

interface Shown {
  saved: ArtifactSourceSavedDto
  kind: ArtifactPullSourceKind
  appId: number
  rotated: boolean
}

export function IntakeSourcesPage() {
  const t = useIntakeText()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const manage = can(OrgPermission.ManageConnections)
  const [editing, setEditing] = useState<ArtifactSourceDto | 'new' | null>(null)
  const [shown, setShown] = useState<Shown | null>(null)
  const sources = useQuery({ queryKey: intakeKeys.sources(organization.Id), queryFn: () => intakeApi.sources(organization.Id) })
  const rows = sources.data ?? []
  const shownUrl = shown ? absoluteUrl(shown.saved.Url ?? '') : ''

  return (
    <IntakeShell
      actions={
        manage ? (
          <Button onClick={() => setEditing('new')}>
            <Plus aria-hidden />
            {t.sources.add}
          </Button>
        ) : null
      }
    >
      <Callout tone="info">{t.sources.intro}</Callout>
      {!manage ? <p className="text-xs text-fg3">{t.manageOnly}</p> : null}
      {sources.isLoading ? (
        <CardSkeleton />
      ) : sources.isError ? (
        <ScreenState error={sources.error} onRetry={() => void sources.refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState icon={<Rss aria-hidden />} title={t.sources.emptyTitle} description={t.sources.emptyBody} />
      ) : (
        <ul className="grid gap-3">
          {rows.map((source) => (
            <SourceCard
              key={source.Id}
              source={source}
              manage={manage}
              onEdit={() => setEditing(source)}
              onSaved={(saved) => setShown({ saved, kind: source.Kind, appId: source.AppId, rotated: true })}
            />
          ))}
        </ul>
      )}
      <section className="card grid gap-2 p-4 text-[13px]">
        <p className="text-fg2">{t.sources.easSignature}</p>
        <p className="text-fg2">{t.sources.signature}</p>
        <p className="text-fg2">{t.sources.responses}</p>
      </section>
      {editing ? (
        <SourceDialog
          source={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={(saved, kind, appId, rotated) => {
            setEditing(null)
            if (saved.Secret) {
              setShown({ saved, kind, appId, rotated })
            } else {
              showToast(t.sources.saved, 'success')
            }
          }}
        />
      ) : null}
      {shown ? (
        <ShownOnceDialog
          title={shown.rotated ? t.sourceForm.rotatedTitle : t.sourceForm.createdTitle}
          warning={t.sourceForm.warning}
          ackLabel={t.sourceForm.ack}
          onClose={() => setShown(null)}
        >
          <div className="grid gap-1">
            <b className="text-[13px]">{t.sources.url}</b>
            <SecretBox value={shownUrl} />
          </div>
          <div className="grid gap-1">
            <b className="text-[13px]">{t.sourceForm.secret}</b>
            <SecretBox value={shown.saved.Secret ?? ''} />
          </div>
          {shown.kind === ArtifactPullSourceKind.Eas ? (
            <Snippet
              title={t.sourceForm.register}
              code={easCommand(shownUrl, shown.saved.Secret ?? '')}
              copyLabel={interpolate(t.setup.copy, { name: t.sourceForm.register })}
            />
          ) : (
            <Snippet
              title={t.sources.sample}
              code={webhookSample(shownUrl, shown.appId)}
              copyLabel={interpolate(t.setup.copy, { name: t.sources.sample })}
            />
          )}
        </ShownOnceDialog>
      ) : null}
    </IntakeShell>
  )
}
