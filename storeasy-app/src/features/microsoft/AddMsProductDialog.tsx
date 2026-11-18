import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Callout, FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Select } from '@/components/ui/Select'
import { Button, Field, Input } from '@/components/ui/primitives'
import { useActivity } from '@/lib/activity/ActivityContext'
import { connectionsApi } from '@/lib/api/endpoints'
import { apiErrorMessage, apiStatus } from '@/lib/api/http'
import { OrgPermission, StoreKind } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { msApi } from './api'
import { useMs } from './i18n/useMs'
import { isStoreId } from './rules'

/**
 * Adds an MSI/EXE product by its Store id (the MSI/EXE submission API cannot list products; MSIX apps come with the
 * sync). The answer carries the sync job that confirms the record; it is followed on the live stream.
 */
export function AddMsProductDialog({ open, onClose, onAdded }: { open: boolean; onClose: () => void; onAdded?: (appId: number) => void }) {
  const base = useT()
  const t = useMs()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const { watchJobs } = useActivity()
  const queryClient = useQueryClient()
  const [connectionId, setConnectionId] = useState<number | null>(null)
  const [productId, setProductId] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [server, setServer] = useState<string | null>(null)

  const connections = useQuery({
    queryKey: queryKeys.connections.list(organization.Id),
    queryFn: () => connectionsApi.list(organization.Id),
    enabled: open,
  })
  const microsoft = (connections.data ?? []).filter((connection) => connection.Store === StoreKind.Microsoft)

  useEffect(() => {
    if (open && connectionId === null && microsoft.length > 0) {
      setConnectionId(microsoft[0].Id)
    }
  }, [open, connectionId, microsoft])

  const close = () => {
    setProductId('')
    setName('')
    setError(null)
    setServer(null)
    setConnectionId(null)
    onClose()
  }

  const add = useMutation({
    mutationFn: () => msApi.addProduct(organization.Id, connectionId ?? 0, productId.trim().toUpperCase(), name.trim() || null),
    meta: { silentError: true },
    onSuccess: (added) => {
      watchJobs([added.JobId])
      void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all })
      showToast(interpolate(t.addProduct.added, { id: productId.trim().toUpperCase() }), 'success')
      onAdded?.(added.AppId)
      close()
    },
    onError: (failure) => setServer(apiStatus(failure) === 409 ? t.addProduct.exists : apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const problem = isStoreId(productId) ? null : t.addProduct.productIdInvalid
    setError(problem)
    setServer(null)
    if (!problem && connectionId !== null) {
      add.mutate()
    }
  }

  const empty = connections.isSuccess && microsoft.length === 0

  return (
    <Modal
      open={open}
      onClose={close}
      title={t.addProduct.title}
      description={t.addProduct.lead}
      footer={
        <>
          <Button variant="ghost" onClick={close}>
            {base.common.cancel}
          </Button>
          <Button
            type="submit"
            form="add-ms-product-form"
            variant="primary"
            loading={add.isPending}
            disabled={empty || connectionId === null}
          >
            {t.addProduct.submit}
          </Button>
        </>
      }
    >
      <form id="add-ms-product-form" className="grid gap-4" onSubmit={submit} noValidate>
        {server ? <FormErrors title={server} /> : null}
        {empty ? (
          <Callout tone="info">
            {t.addProduct.noConnection}{' '}
            {can(OrgPermission.ManageConnections) ? (
              <Link className="lnk" to="/connections">
                {t.addProduct.openConnections}
              </Link>
            ) : null}
          </Callout>
        ) : (
          <Field label={t.addProduct.connection}>
            <Select
              aria-label={t.addProduct.connection}
              value={connectionId}
              options={microsoft.map((connection) => ({ value: connection.Id, label: connection.Name }))}
              onChange={setConnectionId}
            />
          </Field>
        )}
        <Field label={t.addProduct.productId} hint={t.addProduct.productIdHint} error={error}>
          <Input
            mono
            value={productId}
            autoComplete="off"
            spellCheck={false}
            placeholder="9P4K7Q2MZL1D"
            maxLength={12}
            onChange={(event) => setProductId(event.target.value.toUpperCase())}
          />
        </Field>
        <Field label={t.addProduct.name} hint={t.addProduct.nameHint} optional>
          <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
        </Field>
      </form>
    </Modal>
  )
}
