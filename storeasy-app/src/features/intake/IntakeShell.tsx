import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Upload } from 'lucide-react'
import { Callout } from '@/components/ui/Feedback'
import { FileDrop } from '@/components/ui/FileDrop'
import { Modal } from '@/components/ui/Modal'
import { Progress } from '@/components/ui/Progress'
import { Select } from '@/components/ui/Select'
import { Tabs } from '@/components/ui/Tabs'
import { Button, Checkbox, Field, PageHeader, Spinner, Textarea } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { appsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { SortType, type ListRequest } from '@/lib/api/list'
import { OrgPermission } from '@/lib/api/types'
import { UploadAbortedError } from '@/lib/assets/upload'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { useLiveRefresh } from '@/features/notifications/live'
import { intakeApi, intakeKeys } from './api'
import { useIntakeText } from './i18n'
import { PACKAGE_EXTENSIONS } from './labels'
import { uploadArtifact } from './upload'

// The frame every intake screen shares (design 13): heading, the "Setup" and "Upload package" actions, the section tabs,
// and the live refresh. The hub sends artifact.progress for every state change of a session; while the stream is down
// the screens that show running sessions poll instead.

export const ARTIFACT_PROGRESS_EVENT = 'artifact.progress'
const POLL_MS = 5000
const DEFAULT_MAX_BYTES = 4 * 1024 * 1024 * 1024

export const APPS_REQUEST: ListRequest = { PageNumber: 1, RowCount: 100, Sorting: { SortBy: 'Name', Type: SortType.Asc } }

/** The organization's apps (the pickers of every intake screen). */
export function useIntakeApps() {
  const organization = useCurrentOrganization()
  return useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APPS_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APPS_REQUEST),
    select: (response) => response.Data ?? [],
  })
}

/**
 * A refetchInterval for a query that shows running sessions: while the live stream is down and `isActive(data)` says
 * something still runs, the query polls; with the stream connected the artifact.progress events refresh it instead.
 */
export function useLivePoll<T>(isActive: (data: T | undefined) => boolean) {
  const { connection } = useActivity()
  const connected = connection.state === 'connected'
  return (query: { state: { data: T | undefined } }): number | false => (!connected && isActive(query.state.data) ? POLL_MS : false)
}

type UploadState =
  | { kind: 'idle'; error: string | null }
  | { kind: 'hashing'; file: File }
  | { kind: 'uploading'; file: File; loaded: number; total: number }
  | { kind: 'failed'; file: File; message: string }

export function UploadDialog({ initialAppId, onClose }: { initialAppId?: number | null; onClose: () => void }) {
  const t = useIntakeText()
  const base = useT()
  const format = useFormat()
  const navigate = useNavigate()
  const organization = useCurrentOrganization()
  const apps = useIntakeApps()
  const settings = useQuery({
    queryKey: intakeKeys.settings(organization.Id),
    queryFn: () => intakeApi.settings(organization.Id),
  })
  const [appId, setAppId] = useState<number | null>(initialAppId ?? null)
  const [notes, setNotes] = useState('')
  // The web upload holds the package by default, so its checks are seen before anything reaches a store.
  const [hold, setHold] = useState(true)
  const [state, setState] = useState<UploadState>({ kind: 'idle', error: null })
  const controller = useRef<AbortController | null>(null)
  const maxBytes = settings.data?.MaxArtifactBytes ?? DEFAULT_MAX_BYTES

  useEffect(() => () => controller.current?.abort(), [])

  const start = async (file: File) => {
    if (appId === null) {
      return
    }
    const abort = new AbortController()
    controller.current = abort
    setState({ kind: 'hashing', file })
    try {
      const result = await uploadArtifact(organization.Id, appId, file, {
        notes,
        hold,
        signal: abort.signal,
        onHashed: () => setState({ kind: 'uploading', file, loaded: 0, total: file.size }),
        onProgress: ({ loaded, total }) => setState({ kind: 'uploading', file, loaded, total }),
      })
      if (result.duplicate) {
        showToast(interpolate(t.upload.duplicate, { id: result.originalSessionId ?? result.sessionId }), 'info')
      } else {
        showToast(t.upload.done, 'success')
      }
      onClose()
      navigate(`/intake/sessions/${result.sessionId}`)
    } catch (error) {
      if (error instanceof UploadAbortedError) {
        setState({ kind: 'idle', error: null })
      } else {
        setState({ kind: 'failed', file, message: apiErrorMessage(error, t.upload.failed) })
      }
    } finally {
      controller.current = null
    }
  }

  const busy = state.kind === 'hashing' || state.kind === 'uploading'
  return (
    <Modal
      open
      onClose={() => {
        controller.current?.abort()
        onClose()
      }}
      title={t.upload.title}
      description={t.upload.body}
      footer={
        <Button variant="ghost" onClick={onClose} disabled={busy}>
          {base.common.cancel}
        </Button>
      }
    >
      <div className="grid gap-3">
        <Field label={t.upload.app} hint={apps.data?.length === 0 ? t.upload.noApps : undefined}>
          <Select<number>
            value={appId}
            disabled={busy}
            placeholder={base.common.select}
            options={(apps.data ?? []).map((app) => ({ value: app.Id, label: app.Name }))}
            onChange={setAppId}
          />
        </Field>
        <Field label={t.upload.notes} optional>
          <Textarea value={notes} rows={2} maxLength={4000} disabled={busy} onChange={(event) => setNotes(event.target.value)} />
        </Field>
        <div className="grid gap-0.5">
          <Checkbox checked={hold} disabled={busy} onChange={setHold}>
            {t.upload.hold}
          </Checkbox>
          <span className="pl-6 text-xs text-fg3">{t.upload.holdHint}</span>
        </div>
        {state.kind === 'hashing' ? (
          <div className="drop up" role="status">
            <span className="flex items-center gap-2 text-[13px]">
              <Spinner size={16} />
              <b className="truncate">{state.file.name}</b>
            </span>
            <span className="text-xs text-fg3">{t.upload.hashing}</span>
          </div>
        ) : state.kind === 'uploading' ? (
          <div className="drop up" role="status" aria-label={t.upload.uploading}>
            <div className="flex min-w-0 justify-between gap-3 text-[13px]">
              <b className="truncate">{state.file.name}</b>
              <span className="mono flex-none text-fg2">
                {interpolate(t.upload.progress, { done: format.bytes(state.loaded), total: format.bytes(state.total) })}
              </span>
            </div>
            <Progress value={state.loaded} max={state.total || 1} label={state.file.name} />
            <button type="button" className="lnk justify-self-start text-xs" onClick={() => controller.current?.abort()}>
              {t.upload.cancel}
            </button>
          </div>
        ) : state.kind === 'failed' ? (
          <div className="drop rej" role="alert">
            <b>{t.upload.failed}</b>
            <span className="err-t">
              {state.file.name} · {state.message}
            </span>
            <span className="flex gap-2">
              <Button size="sm" onClick={() => void start(state.file)}>
                {t.upload.retry}
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setState({ kind: 'idle', error: null })}>
                {base.common.cancel}
              </Button>
            </span>
          </div>
        ) : appId !== null ? (
          <FileDrop
            label={t.upload.file}
            accept={PACKAGE_EXTENSIONS}
            maxBytes={maxBytes}
            hint={interpolate(t.upload.hint, { size: format.bytes(maxBytes) })}
            error={state.error}
            onFile={(file) => void start(file)}
            onReject={(file, reason) =>
              setState({
                kind: 'idle',
                error:
                  reason === 'type'
                    ? t.upload.unsupported
                    : interpolate(t.upload.tooLarge, { size: format.bytes(file.size), max: format.bytes(maxBytes) }),
              })
            }
          />
        ) : null}
        <Callout tone="info">
          {t.upload.rule}
          {settings.data ? ` ${interpolate(t.upload.retention, { days: settings.data.ArtifactRetentionDays })}` : ''}
        </Callout>
      </div>
    </Modal>
  )
}

export function IntakeShell({ children, actions }: { children: ReactNode; actions?: ReactNode }) {
  const t = useIntakeText()
  const { can } = useOrganization()
  const [uploading, setUploading] = useState(false)
  useLiveRefresh([ARTIFACT_PROGRESS_EVENT], intakeKeys.all)
  return (
    <div className="grid gap-4">
      <PageHeader
        title={t.title}
        subtitle={t.subtitle}
        actions={
          <>
            {actions}
            <Link className="btn" to="/intake/setup">
              {t.setupAction}
            </Link>
            {can(OrgPermission.ReleaseTest) ? (
              <Button variant="primary" onClick={() => setUploading(true)}>
                <Upload aria-hidden />
                {t.uploadAction}
              </Button>
            ) : null}
          </>
        }
      />
      <Tabs
        label={t.tabs.label}
        items={[
          { value: 'overview', label: t.tabs.overview, to: '/intake' },
          { value: 'sessions', label: t.tabs.sessions, to: '/intake/sessions' },
          { value: 'rules', label: t.tabs.rules, to: '/intake/rules' },
          { value: 'publishers', label: t.tabs.publishers, to: '/intake/trusted-publishers' },
          { value: 'tokens', label: t.tabs.tokens, to: '/intake/tokens' },
          { value: 'sources', label: t.tabs.sources, to: '/intake/sources' },
          { value: 'setup', label: t.tabs.setup, to: '/intake/setup' },
          { value: 'settings', label: t.tabs.settings, to: '/intake/settings' },
        ]}
      />
      {children}
      {uploading ? <UploadDialog onClose={() => setUploading(false)} /> : null}
    </div>
  )
}
