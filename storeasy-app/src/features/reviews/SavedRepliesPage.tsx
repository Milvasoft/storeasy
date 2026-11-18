import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, MessageSquareText, Plus, TriangleAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { useConfirm } from '@/components/ui/ConfirmProvider'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Button, CardSkeleton, Checkbox, Chip, EmptyState, Field, Input, PageHeader, RadioGroup, Textarea } from '@/components/ui/primitives'
import { APP_NAMES_REQUEST } from '@/lib/activity/ActivityContext'
import { appsApi } from '@/lib/api/endpoints'
import { OrgPermission } from '@/lib/api/types'
import { useFormat, useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { useCurrentOrganization, useOrganization } from '@/lib/org/OrganizationContext'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { reviewKeys, reviewsApi } from './api'
import { useReviewsText } from './i18n'
import { fillVariables, graphemeCount, REPLY_VARIABLES, unknownVariables } from './text'
import type { LocalizedText, SavedReplyDto } from './types'

// Design 9.9-9.10: the organization's saved replies, one text per language, offered in every app or in chosen ones.

const GOOGLE_LIMIT = 350
const APPLE_LIMIT = 5970
const NAME_MAX = 100

function longest(reply: SavedReplyDto): LocalizedText | null {
  return (reply.Variants ?? []).reduce<LocalizedText | null>(
    (best, variant) => (graphemeCount(variant.Text ?? '') > graphemeCount(best?.Text ?? '') ? variant : best),
    null,
  )
}

function SavedReplyDialog({ reply, onClose }: { reply: SavedReplyDto | null; onClose: () => void }) {
  const t = useReviewsText()
  const base = useT()
  const organization = useCurrentOrganization()
  const queryClient = useQueryClient()
  const confirm = useConfirm()
  const apps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APP_NAMES_REQUEST),
    staleTime: 60_000,
  })
  const [name, setName] = useState(reply?.Name ?? '')
  const [variants, setVariants] = useState<LocalizedText[]>(reply?.Variants?.length ? reply.Variants : [{ Locale: 'tr', Text: '' }])
  const [active, setActive] = useState(0)
  const [newLocale, setNewLocale] = useState('')
  const [scope, setScope] = useState<'all' | 'some'>(reply?.AppIds?.length ? 'some' : 'all')
  const [appIds, setAppIds] = useState<number[]>(reply?.AppIds ?? [])
  const [touched, setTouched] = useState(false)

  const current = variants[active] ?? variants[0]
  const text = current?.Text ?? ''
  const unknown = unknownVariables(text)
  const preview = fillVariables(text, { author: t.saved.sampleAuthor, app: t.saved.sampleApp, version: '3.1.2' })
  const nameError = touched && !name.trim() ? t.saved.nameRequired : null
  const textError = unknown.length > 0 ? interpolate(t.saved.unknownVariable, { names: unknown.map((item) => `{${item}}`).join(', ') }) : null
  const filled = variants.filter((variant) => (variant.Text ?? '').trim() && (variant.Locale ?? '').trim())
  const invalid =
    !name.trim() ||
    filled.length === 0 ||
    variants.some((variant) => unknownVariables(variant.Text ?? '').length > 0) ||
    (scope === 'some' && appIds.length === 0)

  const setText = (value: string) => setVariants((list) => list.map((variant, index) => (index === active ? { ...variant, Text: value } : variant)))

  const done = (message: string) => {
    showToast(message, 'success')
    void queryClient.invalidateQueries({ queryKey: reviewKeys.all })
    onClose()
  }
  const save = useMutation({
    mutationFn: () => {
      const input = { Name: name.trim(), Variants: filled, AppIds: scope === 'all' ? [] : appIds }
      return reply ? reviewsApi.updateSavedReply(organization.Id, reply.Id, input) : reviewsApi.createSavedReply(organization.Id, input)
    },
    onSuccess: () => done(t.saved.savedToast),
  })
  const remove = useMutation({
    mutationFn: () => reviewsApi.deleteSavedReply(organization.Id, reply?.Id ?? 0),
    onSuccess: () => done(t.saved.deletedToast),
  })

  const addLocale = () => {
    const locale = newLocale.trim()
    if (!locale || variants.some((variant) => variant.Locale === locale)) {
      return
    }
    setVariants((list) => [...list, { Locale: locale, Text: '' }])
    setActive(variants.length)
    setNewLocale('')
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={reply ? t.saved.editTitle : t.saved.addTitle}
      description={reply ? interpolate(t.saved.usedTimes, { count: reply.UseCount }) : t.saved.addBody}
      footer={
        <>
          {reply ? (
            <Button
              variant="danger-outline"
              className="mr-auto"
              loading={remove.isPending}
              onClick={async () => {
                if (await confirm({ title: interpolate(t.saved.deleteTitle, { name: reply.Name ?? '' }), danger: true, confirmLabel: t.saved.delete })) {
                  remove.mutate()
                }
              }}
            >
              {t.saved.delete}
            </Button>
          ) : null}
          <Button variant="ghost" onClick={onClose}>
            {base.common.cancel}
          </Button>
          <Button
            variant="primary"
            loading={save.isPending}
            disabled={touched && invalid}
            onClick={() => {
              setTouched(true)
              if (!invalid) {
                save.mutate()
              }
            }}
          >
            {base.common.save}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        <Field label={t.saved.name} error={nameError}>
          <Input value={name} maxLength={NAME_MAX} onChange={(event) => setName(event.target.value)} />
        </Field>
        <div className="toolbar" role="group" aria-label={t.saved.languages}>
          {variants.map((variant, index) => (
            <Chip key={`${variant.Locale}-${index}`} selected={index === active} onClick={() => setActive(index)}>
              {variant.Locale}
            </Chip>
          ))}
          <span className="flex items-center gap-1">
            <Input
              className="w-24"
              aria-label={t.saved.newLocale}
              placeholder="en-US"
              value={newLocale}
              onChange={(event) => setNewLocale(event.target.value)}
            />
            <Button size="sm" onClick={addLocale}>
              <Plus aria-hidden />
              {t.saved.addLocale}
            </Button>
          </span>
        </div>
        <Field
          label={interpolate(t.saved.text, { locale: current?.Locale ?? '' })}
          error={textError}
          hint={interpolate(t.saved.characters, { count: graphemeCount(text) })}
        >
          <Textarea rows={6} value={text} onChange={(event) => setText(event.target.value)} />
        </Field>
        <div className="toolbar" role="group" aria-label={t.saved.insert}>
          <span className="text-xs text-fg3">{t.saved.insert}</span>
          {REPLY_VARIABLES.map((variable) => (
            <Chip key={variable} onClick={() => setText(`${text}{${variable}}`)}>
              {`{${variable}}`}
            </Chip>
          ))}
        </div>
        <div className="rounded-md border border-border p-3 text-[13px]">
          <b className="block text-xs text-fg3">{t.saved.preview}</b>
          <p className="whitespace-pre-wrap">{preview}</p>
          <span className="mt-1 block text-xs text-fg3 tabular-nums">
            {interpolate(t.saved.limits, { length: graphemeCount(preview), google: GOOGLE_LIMIT, apple: APPLE_LIMIT })}
          </span>
        </div>
        <RadioGroup
          name="saved-reply-scope"
          label={t.saved.scope}
          value={scope}
          options={[
            { value: 'all', label: t.saved.scopeAll },
            { value: 'some', label: t.saved.scopeSome },
          ]}
          onChange={setScope}
        />
        {scope === 'some' ? (
          <div className="grid gap-1.5" role="group" aria-label={t.saved.scopeSome}>
            {(apps.data?.Data ?? []).map((app) => (
              <Checkbox
                key={app.Id}
                checked={appIds.includes(app.Id)}
                onChange={(checked) => setAppIds((ids) => (checked ? [...ids, app.Id] : ids.filter((id) => id !== app.Id)))}
              >
                {app.Name}
              </Checkbox>
            ))}
          </div>
        ) : null}
      </div>
    </Modal>
  )
}

/** /reviews/saved-replies. */
export function SavedRepliesPage() {
  const t = useReviewsText()
  const format = useFormat()
  const organization = useCurrentOrganization()
  const { can } = useOrganization()
  const editor = can(OrgPermission.ReplyReviews)
  const [editing, setEditing] = useState<SavedReplyDto | 'new' | null>(null)
  const replies = useQuery({
    queryKey: reviewKeys.savedReplies(organization.Id, null, null),
    queryFn: () => reviewsApi.savedReplies(organization.Id),
  })
  const apps = useQuery({
    queryKey: queryKeys.apps.list(organization.Id, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organization.Id, APP_NAMES_REQUEST),
    staleTime: 60_000,
  })
  const appName = (id: number) => apps.data?.Data.find((app) => app.Id === id)?.Name ?? `#${id}`

  const columns: Column<SavedReplyDto>[] = [
    {
      key: 'name',
      header: t.saved.name,
      role: 'main',
      cell: (reply) => {
        const long = longest(reply)
        const tooLong = long && graphemeCount(long.Text ?? '') > GOOGLE_LIMIT
        return (
          <span className="grid gap-0.5">
            <b>{reply.Name}</b>
            <span className="line-clamp-1 text-xs text-fg3">{reply.Variants?.[0]?.Text}</span>
            {tooLong ? (
              <span className="inline-flex items-center gap-1 text-xs text-warningText">
                <TriangleAlert className="h-3.5 w-3.5" aria-hidden />
                {interpolate(t.saved.tooLong, { locale: long.Locale ?? '', count: graphemeCount(long.Text ?? '') })}
              </span>
            ) : null}
          </span>
        )
      },
    },
    {
      key: 'languages',
      header: t.saved.languages,
      cell: (reply) => (
        <span className="flex flex-wrap gap-1">
          {(reply.Variants ?? []).map((variant) => (
            <Badge key={variant.Locale} small>
              {variant.Locale}
            </Badge>
          ))}
        </span>
      ),
    },
    {
      key: 'apps',
      header: t.saved.apps,
      cell: (reply) => (reply.AppIds?.length ? reply.AppIds.map(appName).join(', ') : t.saved.scopeAll),
    },
    { key: 'uses', header: t.saved.uses, cell: (reply) => <span className="tabular-nums">{reply.UseCount}</span> },
    {
      key: 'updated',
      header: t.saved.updated,
      role: 'hidePhone',
      cell: (reply) => [reply.UpdatedByUserName, reply.UpdatedDate ? format.date(reply.UpdatedDate) : null].filter(Boolean).join(' · ') || '-',
    },
  ]

  return (
    <>
      <Link className="lnk mb-3 inline-flex items-center gap-1 text-[13px]" to="/reviews">
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
        {t.title}
      </Link>
      <PageHeader
        title={t.saved.title}
        subtitle={t.saved.subtitle}
        actions={
          editor ? (
            <Button variant="primary" onClick={() => setEditing('new')}>
              <Plus aria-hidden />
              {t.saved.add}
            </Button>
          ) : null
        }
      />
      {replies.isLoading ? (
        <CardSkeleton />
      ) : replies.isError ? (
        <ScreenState error={replies.error} onRetry={() => void replies.refetch()} />
      ) : (replies.data ?? []).length === 0 ? (
        <EmptyState icon={<MessageSquareText aria-hidden />} title={t.saved.emptyTitle} description={t.saved.emptyBody} />
      ) : (
        <DataTable
          caption={t.saved.title}
          columns={columns}
          rows={replies.data ?? []}
          rowKey={(reply) => reply.Id}
          onRowClick={editor ? (reply) => setEditing(reply) : undefined}
        />
      )}
      {editing ? <SavedReplyDialog reply={editing === 'new' ? null : editing} onClose={() => setEditing(null)} /> : null}
    </>
  )
}
