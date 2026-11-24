import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { FormErrors } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Field, PasswordInput } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { ConnectionStatus } from '@/lib/api/types'
import { useFeature } from '@/lib/features/FeatureFlagsContext'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { useCurrentOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { easApi } from './api'
import { useEas } from './i18n/useEas'
import { easKeys } from './keys'

// The organization's Expo access token on the connections page (Manage connections). The token is typed once, sealed by
// storeasy and never shown again; the card shows the Expo actor it belongs to.

function TokenDialog({ onClose }: { onClose: () => void }) {
  const t = useEas()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [token, setToken] = useState('')
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => easApi.saveConnection(organization.Id, token.trim()),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.connection.saved, 'success')
      void queryClient.invalidateQueries({ queryKey: easKeys.all })
      onClose()
    },
    onError: (failure) => setError(apiErrorMessage(failure, base.common.error)),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!token.trim()) {
      setError(t.connection.tokenRequired)
      return
    }
    setError(null)
    save.mutate()
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={t.connection.add}
      description={t.connection.intro}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button type="submit" form="expo-token-form" variant="primary" loading={save.isPending}>
            {t.connection.save}
          </Button>
        </>
      }
    >
      <form id="expo-token-form" className="grid gap-4" onSubmit={submit} noValidate>
        {error ? <FormErrors title={error} /> : null}
        <Field label={t.connection.token}>
          <PasswordInput value={token} autoComplete="off" maxLength={500} onChange={(event) => setToken(event.target.value)} />
        </Field>
      </form>
    </Modal>
  )
}

function Section() {
  const t = useEas()
  const base = useT()
  const format = useFormat()
  const confirm = useConfirm()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const [adding, setAdding] = useState(false)

  const connection = useQuery({ queryKey: easKeys.connection(organization.Id), queryFn: () => easApi.connection(organization.Id) })

  const remove = useMutation({
    mutationFn: () => easApi.removeConnection(organization.Id),
    meta: { silentError: true },
    onSuccess: () => {
      showToast(t.connection.removed, 'success')
      void queryClient.invalidateQueries({ queryKey: easKeys.all })
    },
    onError: (failure) => showToast(apiErrorMessage(failure, base.common.error), 'error'),
  })

  const askRemove = async () => {
    if (
      await confirm({ title: t.connection.removeTitle, message: t.connection.removeBody, confirmLabel: t.connection.remove, danger: true })
    ) {
      remove.mutate()
    }
  }

  let body
  if (connection.isPending) {
    body = <CardSkeleton count={1} />
  } else if (connection.isError) {
    body = <ScreenState error={connection.error} onRetry={() => void connection.refetch()} />
  } else if (!connection.data) {
    body = <p className="text-fg3">{t.connection.none}</p>
  } else {
    body = (
      <p className="inline-flex flex-wrap items-center gap-2">
        {t.connection.owner}: <span className="mono">{connection.data.Fingerprint}</span>
        <Badge small tone={connection.data.Status === ConnectionStatus.Verified ? 'success' : 'danger'}>
          {connection.data.LastVerifiedDate ? format.date(connection.data.LastVerifiedDate) : '-'}
        </Badge>
      </p>
    )
  }

  return (
    <section aria-label={t.connection.title} className="card mt-6">
      <div className="card-h flex-wrap">
        <h2 className="h3 mr-auto">{t.connection.title}</h2>
        <Button variant="outline" onClick={() => setAdding(true)}>
          <Plus aria-hidden />
          {connection.data ? t.connection.replace : t.connection.add}
        </Button>
        {connection.data ? (
          <Button variant="ghost" aria-label={t.connection.remove} onClick={() => void askRemove()}>
            <Trash2 aria-hidden />
          </Button>
        ) : null}
      </div>
      <div className="card-b grid gap-2">
        <p className="text-fg2">{t.connection.intro}</p>
        {body}
      </div>
      {adding ? <TokenDialog onClose={() => setAdding(false)} /> : null}
    </section>
  )
}

/** The Expo connection section of the connections page; hidden while the EasBuilds switch is off. */
export function ExpoConnectionSection() {
  return useFeature('EasBuilds') ? <Section /> : null
}
