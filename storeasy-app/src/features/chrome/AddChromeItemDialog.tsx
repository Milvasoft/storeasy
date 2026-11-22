import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { appsApi, connectionsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { StoreKind } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { chromeApi } from './api'
import { useChrome } from './i18n/useChrome'
import { isItemId } from './rules'

// The apps an item can join (the first 100 by the list's own order).
const APPS_REQUEST = { PageNumber: 1, RowCount: 100 }

/**
 * Adds a Chrome Web Store item by its extension id (the API cannot list a publisher's items or create one) to an existing
 * app or to a new app. The answer carries the sync job that reads the item; it is followed on the live stream.
 */
export function AddChromeItemDialog({ open, onClose, onAdded }: { open: boolean; onClose: () => void; onAdded?: (appId: number) => void }) {
  const base = useT()
  const t = useChrome()
  const organization = useCurrentOrganization()
  const { watchJobs } = useActivity()
  const queryClient = useQueryClient()
  const [connectionId, setConnectionId] = useState<number | null>(null)
  const [identifier, setIdentifier] = useState('')
  // 0: a new app.
  const [appId, setAppId] = useState<number>(0)
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
    enabled: open,
  })
  const apps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APPS_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APPS_REQUEST),
    enabled: open,
  })
  const chrome = (connections.data ?? []).filter((connection) => connection.Store === StoreKind.Chrome)

  useEffect(() => {
    if (open && connectionId === null && chrome.length > 0) {
      setConnectionId(chrome[0].Id)
    }
  }, [open, connectionId, chrome])

  const close = () => {
    setIdentifier('')
    setName('')
    setAppId(0)
    setError(null)
    setServer(null)
    setConnectionId(null)
    onClose()
  }

  const add = useMutation({
    mutationFn: () => chromeApi.addItem(organization.Id, connectionId ?? 0, identifier.trim(), appId || null, name.trim() || null),
    meta: { silentError: true },
    onSuccess: (added) => {
      watchJobs([added.JobId])
      void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
      showToast(t.add.added, 'success', identifier.trim())
      onAdded?.(added.AppId)
      close()
    },
    onError: (failure) => setServer(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = isItemId(identifier) ? null : t.add.itemIdInvalid
    setError(problem)
    setServer(null)
    if (!problem && connectionId !== null) {
      add.mutate()
    }
  }

  const empty = connections.isSuccess && chrome.length === 0
  const appOptions = [{ value: 0, label: t.add.newApp }, ...(apps.data?.Data ?? []).map((app) => ({ value: app.Id, label: app.Name }))]

  return (
    <Modal
      open={open}
      onClose={close}
      title={t.add.title}
      description={t.add.body}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {base.common.cancel}
          </Button>
          <Button
            type="submit"
            form="add-chrome-item-form"
            variant="primary"
            loading={add.isPending}
            disabled={empty || connectionId === null}
          >
            {t.add.submit}
          </Button>
        </>
      }
    >
      <form id="add-chrome-item-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        {empty ? (
          <Callout tone="info">{t.add.noConnection}</Callout>
        ) : (
          <Field label={t.add.connection}>
            <Select
              aria-label={t.add.connection}
              value={connectionId}
              options={chrome.map((connection) => ({ value: connection.Id, label: connection.Name }))}
              onChange={setConnectionId}
            />
          </Field>
        )}
        <Field label={t.add.itemId} hint={t.add.itemIdHint} error={error}>
          <Input
            mono
            value={identifier}
            autoComplete="off"
            spellCheck={false}
            placeholder="abcdefghijklmnopabcdefghijklmnop"
            maxLength={32}
            onChange={(event) => setIdentifier(event.target.value)}
          />
        </Field>
        <Field label={t.add.app} hint={t.add.appHint}>
          <Select aria-label={t.add.app} value={appId} options={appOptions} onChange={setAppId} searchable />
        </Field>
        {appId === 0 ? (
          <Field label={t.add.name} optional>
            <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
          </Field>
        ) : null}
      </form>
    </Modal>
  )
}
