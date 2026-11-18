import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { storeName } from '@/components/ui/Badge'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { appsApi, connectionsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, apiStatus } from '@/lib/api/http'
import { OrgPermission, StoreKind, type AppListDto, type AppStoreRecordDto } from '@/lib/api/types'
import { isValidPackageName, linkCandidates } from '@/lib/apps/apps'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'

/**
 * Adds a Google Play app by its package name (Play has no API that lists apps). The answer carries the sync job that
 * confirms the record; it is followed on the live stream and announced when it ends.
 */
export function AddGoogleAppDialog({ open, onClose, onAdded }: { open: boolean; onClose: () => void; onAdded?: (appId: number) => void }) {
  const t = useT()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const { watchJobs } = useActivity()
  const queryClient = useQueryClient()
  const [connectionId, setConnectionId] = useState<number | null>(null)
  const [packageName, setPackageName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
    enabled: open,
  })
  const google = (connections.data ?? []).filter((connection) => connection.Store === StoreKind.Google)

  useEffect(() => {
    if (open && connectionId === null && google.length > 0) {
      setConnectionId(google[0].Id)
    }
  }, [open, connectionId, google])

  const close = () => {
    setPackageName('')
    setError(null)
    setServer(null)
    setConnectionId(null)
    onClose()
  }

  const add = useMutation({
    mutationFn: () => appsApi.addGoogle(organization.Id, connectionId ?? 0, packageName.trim()),
    meta: { silentError: true },
    onSuccess: (added) => {
      watchJobs([added.JobId])
      void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
      showToast(interpolate(t.addGoogle.added, { name: packageName.trim() }), 'success')
      onAdded?.(added.AppId)
      close()
    },
    onError: (failure) => setServer(apiStatus(failure) === 409 ? t.addGoogle.exists : apiErrorMessage(failure, t.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = isValidPackageName(packageName) ? null : t.addGoogle.packageInvalid
    setError(problem)
    setServer(null)
    if (!problem && connectionId !== null) {
      add.mutate()
    }
  }

  const empty = connections.isSuccess && google.length === 0

  return (
    <Modal
      open={open}
      onClose={close}
      title={t.addGoogle.title}
      description={t.addGoogle.lead}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="add-google-form" variant="primary" loading={add.isPending} disabled={empty || connectionId === null}>
            {t.addGoogle.submit}
          </Button>
        </>
      }
    >
      <form id="add-google-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        {empty ? (
          <Callout tone="info">
            {t.addGoogle.noConnection}{' '}
            {can(OrgPermission.ManageConnections) ? (
              <Link className="lnk" to="/connections">
                {t.sections.apps.action}
              </Link>
            ) : null}
          </Callout>
        ) : (
          <Field label={t.addGoogle.connection}>
            <Select
              aria-label={t.addGoogle.connection}
              value={connectionId}
              options={google.map((connection) => ({ value: connection.Id, label: connection.Name }))}
              onChange={setConnectionId}
            />
          </Field>
        )}
        <Field label={t.addGoogle.packageName} hint={t.addGoogle.packageHint} error={error}>
          <Input
            mono
            value={packageName}
            autoComplete="off"
            spellCheck={false}
            placeholder="com.example.app"
            maxLength={150}
            onChange={(event) => setPackageName(event.target.value)}
          />
        </Field>
      </form>
    </Modal>
  )
}

function recordLabel(app: AppListDto, record: AppStoreRecordDto): string {
  return [app.Name, record.BundleId ?? record.ExternalId].filter(Boolean).join(' · ')
}

/** Pairs a record of another app with this one (POST app/link), e.g. the Android record of the same product (design 6.3). */
export function LinkRecordDialog({
  app,
  others,
  open,
  onClose,
}: {
  app: AppListDto
  others: AppListDto[]
  open: boolean
  onClose: () => void
}) {
  const t = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [storeAppId, setStoreAppId] = useState<number | null>(null)
  const candidates = linkCandidates(app, others)

  const close = () => {
    setStoreAppId(null)
    onClose()
  }

  const link = useMutation({
    mutationFn: (id: number) => appsApi.link(organization.Id, app.Id, id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
      showToast(t.app.linkDone, 'success')
      close()
    },
  })

  return (
    <Modal
      open={open}
      onClose={close}
      title={interpolate(t.app.linkTitle, { app: app.Name })}
      description={t.app.linkBody}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {t.common.cancel}
          </Button>
          <Button
            variant="primary"
            loading={link.isPending}
            disabled={storeAppId === null}
            onClick={() => storeAppId !== null && link.mutate(storeAppId)}
          >
            {t.app.linkSubmit}
          </Button>
        </>
      }
    >
      {candidates.length === 0 ? (
        <Callout tone="info">{t.app.linkNone}</Callout>
      ) : (
        <div className="grid gap-3">
          <Field label={t.app.linkField} hint={t.app.linkHint}>
            <Select
              aria-label={t.app.linkField}
              searchable
              value={storeAppId}
              placeholder={t.common.select}
              options={candidates.map(({ app: owner, record }) => ({
                value: record.StoreAppId,
                label: recordLabel(owner, record),
                description: storeName(record.Store, t.enums.store),
              }))}
              onChange={setStoreAppId}
            />
          </Field>
        </div>
      )}
    </Modal>
  )
}
