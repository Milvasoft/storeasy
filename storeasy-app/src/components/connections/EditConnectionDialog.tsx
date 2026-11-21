import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { isStoreIconKey, type StoreIconKey } from '@/components/ui/StoreIcons'
import { Button, Field, Input } from '@/components/ui/primitives'
import { connectionsApi } from '@/lib/api/endpoints'
import { apiErrorMessage } from '@/lib/api/http'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import type { StoreConnectionDto } from '@/lib/api/types'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { StoreIconPicker } from './StoreIconPicker'

/**
 * Renames a store connection and changes its icon. The key is not part of this form (an Apple or Google key is replaced
 * with "Replace key", a Microsoft secret with "Renew secret"). Mount it with a key per connection so the fields start
 * from that connection.
 */
export function EditConnectionDialog({
  connection,
  organizationId,
  onClose,
}: {
  connection: StoreConnectionDto | null
  organizationId: number
  onClose: () => void
}) {
  const t = useT()
  const queryClient = useQueryClient()
  const [name, setName] = useState(connection?.Name ?? '')
  const startIcon = connection?.Icon
  const [icon, setIcon] = useState<StoreIconKey | null>(isStoreIconKey(startIcon) ? startIcon : null)
  const [submitted, setSubmitted] = useState(false)
  const [server, setServer] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => connectionsApi.update(organizationId, connection?.Id ?? 0, { Name: name.trim(), Icon: icon }),
    meta: { silentError: true },
    onSuccess: (updated) => {
      queryClient.setQueryData<StoreConnectionDto[]>(queryKeys.connections.list(organizationId), (list) =>
        list?.map((item) => (item.Id === updated.Id ? updated : item)),
      )
      void queryClient.invalidateQueries({ queryKey: queryKeys.connections.all })
      showToast(t.connections.updated, 'success', updated.Name)
      onClose()
    },
    onError: (error) => setServer(apiErrorMessage(error, t.connections.saveFailed)),
  })

  const nameError = submitted && !name.trim() ? t.connections.nameRequired : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    setServer(null)
    if (!name.trim()) {
      return
    }
    save.mutate()
  }

  return (
    <Modal
      open={!!connection}
      onClose={onClose}
      title={interpolate(t.connections.editTitle, { name: connection?.Name ?? '' })}
      description={t.connections.editBody}
      size="lg"
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.common.cancel}
          </Button>
          <Button type="submit" form="edit-connection" variant="primary" loading={save.isPending}>
            {t.common.save}
          </Button>
        </>
      }
    >
      {connection ? (
        <form id="edit-connection" className="grid gap-4" onSubmit={submit} noValidate>
          {server ? <FormErrors title={server} /> : null}
          <Field label={t.connections.name} error={nameError} hint={t.connections.nameHint}>
            <Input
              value={name}
              name="storeasy-connection-name"
              autoComplete="off"
              data-1p-ignore
              data-lpignore="true"
              maxLength={TEXT_LIMITS.connectionName}
              onChange={(event) => setName(event.target.value)}
            />
          </Field>
          <StoreIconPicker store={connection.Store} value={icon} onChange={setIcon} />
        </form>
      ) : null}
    </Modal>
  )
}
