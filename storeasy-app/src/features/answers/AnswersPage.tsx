import { useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Plus, X } from 'lucide-react'
import { CodeBlock } from '@/components/ui/Code'
import { Modal } from '@/components/ui/Modal'
import { ScreenState } from '@/components/ui/ScreenState'
import { Select } from '@/components/ui/Select'
import { Tabs } from '@/components/ui/Tabs'
import {
  Button,
  CardSkeleton,
  Checkbox,
  Field,
  IconButton,
  Input,
  PageHeader,
  PasswordInput,
  Switch,
  Textarea,
} from '@/components/ui/primitives'
import { APP_NAMES_REQUEST } from '@/lib/activity/ActivityContext'
import { appsApi } from '@/lib/api/endpoints'
import { useFormat } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { queryKeys } from '@/lib/query/keys'
import { showToast } from '@/lib/toast/toast'
import { answersApi } from '@/features/common/api'
import { AppWorkspace, type WorkspaceContext } from '@/features/common/AppWorkspace'
import { useP2 } from '@/features/common/i18n/useP2'
import { contentKeys } from '@/features/common/keys'
import { ANSWER_KINDS, answerKindLabel } from '@/features/common/labels'
import { PlanDialog } from '@/features/common/PlanView'
import { splitList } from '@/features/common/text'
import {
  SavedAnswerKind,
  type ChangePlanDto,
  type DataSafetyRow,
  type PrivacyDataTypeAnswer,
  type SavedAnswerSetDto,
} from '@/features/common/types'
import {
  dataSafetyCsv,
  importDataSafetyCsv,
  readContentRating,
  readDataSafety,
  readPrivacy,
  readReviewLogin,
  readReviewNotes,
  readTargetAudience,
} from './answerDocuments'

// Design 6.14-6.15: the saved answers of the forms without an API, one document per kind. The demo account password is
// write only: it is sealed by the API and never comes back (only DemoPasswordSet does).

interface FormProps {
  context: WorkspaceContext
  set: SavedAnswerSetDto
  answers: unknown
}

function useSaveAnswers(context: WorkspaceContext, kind: SavedAnswerKind) {
  const t = useP2()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ answers, password, remove }: { answers: unknown; password?: string | null; remove?: boolean }) =>
      answersApi.update(context.organizationId, context.app.Id, kind, answers, password ?? null, remove ?? false),
    onSuccess: () => {
      showToast(t.answers.saved, 'success', answerKindLabel(kind, t))
      void queryClient.invalidateQueries({ queryKey: contentKeys.answers(context.organizationId, context.app.Id) })
    },
  })
}

function FormShell({
  onSubmit,
  editor,
  loading,
  children,
  extra,
}: {
  onSubmit: () => void
  editor: boolean
  loading: boolean
  children: ReactNode
  extra?: ReactNode
}) {
  const t = useP2()
  return (
    <form
      className="grid gap-4"
      noValidate
      autoComplete="off"
      onSubmit={(event: FormEvent) => {
        event.preventDefault()
        onSubmit()
      }}
    >
      <fieldset className="grid gap-4" disabled={!editor}>
        {children}
      </fieldset>
      {editor ? (
        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" variant="primary" loading={loading}>
            {t.answers.save}
          </Button>
          {extra}
        </div>
      ) : null}
    </form>
  )
}

function PrivacyForm({ context, answers }: FormProps) {
  const t = useP2()
  const initial = readPrivacy(answers)
  const [collects, setCollects] = useState(initial.CollectsData)
  const [url, setUrl] = useState(initial.PrivacyPolicyUrl ?? '')
  const [types, setTypes] = useState<PrivacyDataTypeAnswer[]>(initial.DataTypes)
  const save = useSaveAnswers(context, SavedAnswerKind.PrivacyLabels)
  const update = (index: number, patch: Partial<PrivacyDataTypeAnswer>) =>
    setTypes((list) => list.map((item, i) => (i === index ? { ...item, ...patch } : item)))
  return (
    <FormShell
      editor={context.editor}
      loading={save.isPending}
      onSubmit={() =>
        save.mutate({ answers: { CollectsData: collects, PrivacyPolicyUrl: url.trim() || null, DataTypes: collects ? types : [] } })
      }
    >
      <Checkbox checked={collects} onChange={setCollects}>
        {t.answers.collectsData}
      </Checkbox>
      <Field label={t.answers.privacyUrl}>
        <Input type="url" value={url} onChange={(event) => setUrl(event.target.value)} />
      </Field>
      {collects ? (
        <section className="grid gap-3" aria-label={t.answers.dataTypes}>
          <h3 className="h3">{t.answers.dataTypes}</h3>
          {types.map((type, index) => (
            <div key={index} className="card">
              <div className="card-b grid gap-3 sm:grid-cols-2">
                <Field label={t.answers.category}>
                  <Input value={type.Category} onChange={(event) => update(index, { Category: event.target.value })} />
                </Field>
                <Field label={t.answers.type}>
                  <Input value={type.Type} onChange={(event) => update(index, { Type: event.target.value })} />
                </Field>
                <Field label={t.answers.purposes}>
                  <Input
                    value={type.Purposes.join(', ')}
                    onChange={(event) => update(index, { Purposes: splitList(event.target.value) })}
                  />
                </Field>
                <div className="flex flex-wrap items-center gap-4">
                  <Checkbox checked={type.LinkedToUser} onChange={(value) => update(index, { LinkedToUser: value })}>
                    {t.answers.linked}
                  </Checkbox>
                  <Checkbox checked={type.UsedForTracking} onChange={(value) => update(index, { UsedForTracking: value })}>
                    {t.answers.tracking}
                  </Checkbox>
                  <IconButton
                    label={interpolate(t.answers.removeType, { index: index + 1 })}
                    onClick={() => setTypes((list) => list.filter((_, i) => i !== index))}
                  >
                    <X aria-hidden />
                  </IconButton>
                </div>
              </div>
            </div>
          ))}
          <div>
            <Button
              size="sm"
              onClick={() =>
                setTypes((list) => [...list, { Category: '', Type: '', Purposes: [], LinkedToUser: false, UsedForTracking: false }])
              }
            >
              <Plus aria-hidden />
              {t.answers.addType}
            </Button>
          </div>
        </section>
      ) : null}
    </FormShell>
  )
}

function DataSafetyForm({ context, answers }: FormProps) {
  const t = useP2()
  const [rows, setRows] = useState<DataSafetyRow[]>(readDataSafety(answers).Rows)
  const [csv, setCsv] = useState('')
  const [csvError, setCsvError] = useState<string | null>(null)
  const [plan, setPlan] = useState<ChangePlanDto | null>(null)
  const save = useSaveAnswers(context, SavedAnswerKind.DataSafety)
  const planSend = useMutation({ mutationFn: () => answersApi.planDataSafety(context.organizationId, context.app.Id), onSuccess: setPlan })
  const update = (index: number, patch: Partial<DataSafetyRow>) =>
    setRows((list) => list.map((row, i) => (i === index ? { ...row, ...patch } : row)))
  const cell = (value: string) => value.trim() || null
  return (
    <>
      <FormShell
        editor={context.editor}
        loading={save.isPending}
        onSubmit={() => save.mutate({ answers: { Rows: rows.filter((row) => row.QuestionId.trim()) } })}
        extra={
          <Button loading={planSend.isPending} onClick={() => planSend.mutate()}>
            {t.answers.planDataSafety}
          </Button>
        }
      >
        <Field label={t.answers.importCsv} error={csvError} optional>
          <Textarea rows={3} mono value={csv} onChange={(event) => setCsv(event.target.value)} />
        </Field>
        <div>
          <Button
            size="sm"
            disabled={!csv.trim()}
            onClick={() => {
              const imported = importDataSafetyCsv(csv)
              if (!imported) {
                setCsvError(t.answers.importFailed)
                return
              }
              setRows(imported)
              setCsv('')
              setCsvError(null)
              showToast(interpolate(t.answers.imported, { count: imported.length }), 'success')
            }}
          >
            {t.answers.importApply}
          </Button>
        </div>
        <section aria-label={t.answers.rows} className="grid gap-2">
          {rows.map((row, index) => (
            <div key={index} className="grid items-end gap-2 sm:grid-cols-[1fr_1fr_1fr_1fr_auto]">
              <Field label={t.answers.questionId}>
                <Input mono value={row.QuestionId} onChange={(event) => update(index, { QuestionId: event.target.value })} />
              </Field>
              <Field label={t.answers.responseId}>
                <Input mono value={row.ResponseId ?? ''} onChange={(event) => update(index, { ResponseId: cell(event.target.value) })} />
              </Field>
              <Field label={t.answers.responseValue}>
                <Input value={row.ResponseValue ?? ''} onChange={(event) => update(index, { ResponseValue: cell(event.target.value) })} />
              </Field>
              <Field label={t.answers.label}>
                <Input value={row.Label ?? ''} onChange={(event) => update(index, { Label: cell(event.target.value) })} />
              </Field>
              <IconButton
                label={interpolate(t.answers.removeRow, { index: index + 1 })}
                onClick={() => setRows((list) => list.filter((_, i) => i !== index))}
              >
                <X aria-hidden />
              </IconButton>
            </div>
          ))}
          <div>
            <Button
              size="sm"
              onClick={() => setRows((list) => [...list, { QuestionId: '', ResponseId: null, ResponseValue: null, Label: null }])}
            >
              <Plus aria-hidden />
              {t.answers.addRow}
            </Button>
          </div>
        </section>
      </FormShell>
      <div className="mt-4">
        <CodeBlock label={t.answers.csvPreview}>{dataSafetyCsv(rows.filter((row) => row.QuestionId.trim()))}</CodeBlock>
      </div>
      <PlanDialog plan={plan} onClose={() => setPlan(null)} />
    </>
  )
}

function ContentRatingForm({ context, answers }: FormProps) {
  const t = useP2()
  const initial = readContentRating(answers)
  const [category, setCategory] = useState(initial.Category ?? '')
  const [email, setEmail] = useState(initial.Email ?? '')
  const [pairs, setPairs] = useState<Array<[string, string]>>(Object.entries(initial.Answers))
  const save = useSaveAnswers(context, SavedAnswerKind.ContentRating)
  return (
    <FormShell
      editor={context.editor}
      loading={save.isPending}
      onSubmit={() =>
        save.mutate({
          answers: {
            Category: category.trim() || null,
            Email: email.trim() || null,
            Answers: Object.fromEntries(pairs.filter(([key]) => key.trim()).map(([key, value]) => [key.trim(), value])),
          },
        })
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t.answers.ratingCategory}>
          <Input value={category} onChange={(event) => setCategory(event.target.value)} />
        </Field>
        <Field label={t.answers.ratingEmail}>
          <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </Field>
      </div>
      {pairs.map(([key, value], index) => (
        <div key={index} className="grid items-end gap-2 sm:grid-cols-[1fr_1fr_auto]">
          <Field label={t.answers.question}>
            <Input
              mono
              value={key}
              onChange={(event) => setPairs((list) => list.map((pair, i) => (i === index ? [event.target.value, pair[1]] : pair)))}
            />
          </Field>
          <Field label={t.answers.answer}>
            <Input
              value={value}
              onChange={(event) => setPairs((list) => list.map((pair, i) => (i === index ? [pair[0], event.target.value] : pair)))}
            />
          </Field>
          <IconButton
            label={interpolate(t.answers.removeAnswer, { index: index + 1 })}
            onClick={() => setPairs((list) => list.filter((_, i) => i !== index))}
          >
            <X aria-hidden />
          </IconButton>
        </div>
      ))}
      <div>
        <Button size="sm" onClick={() => setPairs((list) => [...list, ['', '']])}>
          <Plus aria-hidden />
          {t.answers.addAnswer}
        </Button>
      </div>
    </FormShell>
  )
}

function TargetAudienceForm({ context, answers }: FormProps) {
  const t = useP2()
  const initial = readTargetAudience(answers)
  const [groups, setGroups] = useState(initial.AgeGroups.join(', '))
  const [children, setChildren] = useState(initial.AppealsToChildren)
  const [ads, setAds] = useState(initial.ContainsAds)
  const save = useSaveAnswers(context, SavedAnswerKind.TargetAudience)
  return (
    <FormShell
      editor={context.editor}
      loading={save.isPending}
      onSubmit={() => save.mutate({ answers: { AgeGroups: splitList(groups), AppealsToChildren: children, ContainsAds: ads } })}
    >
      <Field label={t.answers.ageGroups}>
        <Input value={groups} onChange={(event) => setGroups(event.target.value)} />
      </Field>
      <Checkbox checked={children} onChange={setChildren}>
        {t.answers.appealsToChildren}
      </Checkbox>
      <Checkbox checked={ads} onChange={setAds}>
        {t.answers.containsAds}
      </Checkbox>
    </FormShell>
  )
}

function ReviewLoginForm({ context, set, answers }: FormProps) {
  const t = useP2()
  const initial = readReviewLogin(answers)
  const [form, setForm] = useState({
    ContactFirstName: initial.ContactFirstName ?? '',
    ContactLastName: initial.ContactLastName ?? '',
    ContactEmail: initial.ContactEmail ?? '',
    ContactPhone: initial.ContactPhone ?? '',
    DemoAccountName: initial.DemoAccountName ?? '',
  })
  const [required, setRequired] = useState(initial.DemoAccountRequired)
  // Write only: never filled from the API, cleared after every save.
  const [password, setPassword] = useState('')
  const [remove, setRemove] = useState(false)
  const save = useSaveAnswers(context, SavedAnswerKind.ReviewLogin)
  const input = (key: keyof typeof form, label: string, type = 'text') => (
    <Field label={label}>
      <Input type={type} value={form[key]} onChange={(event) => setForm((value) => ({ ...value, [key]: event.target.value }))} />
    </Field>
  )
  return (
    <FormShell
      editor={context.editor}
      loading={save.isPending}
      onSubmit={() =>
        save.mutate(
          {
            answers: {
              ContactFirstName: form.ContactFirstName.trim() || null,
              ContactLastName: form.ContactLastName.trim() || null,
              ContactEmail: form.ContactEmail.trim() || null,
              ContactPhone: form.ContactPhone.trim() || null,
              DemoAccountRequired: required,
              DemoAccountName: form.DemoAccountName.trim() || null,
            },
            password: remove ? null : password || null,
            remove,
          },
          { onSuccess: () => setPassword('') },
        )
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        {input('ContactFirstName', t.reviewInfo.firstName)}
        {input('ContactLastName', t.reviewInfo.lastName)}
        {input('ContactEmail', t.reviewInfo.email, 'email')}
        {input('ContactPhone', t.reviewInfo.phone, 'tel')}
      </div>
      <span className="flex items-center gap-2 text-[13px]">
        <Switch checked={required} onChange={setRequired} label={t.reviewInfo.demoRequired} />
        {t.reviewInfo.demoRequired}
      </span>
      {required ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {input('DemoAccountName', t.reviewInfo.demoName)}
          <Field
            label={t.answers.demoPassword}
            hint={`${set.DemoPasswordSet ? t.answers.demoPasswordSet : t.answers.demoPasswordNotSet} ${t.answers.demoPasswordNote}`}
          >
            <PasswordInput
              value={password}
              autoComplete="new-password"
              disabled={remove}
              onChange={(event) => setPassword(event.target.value)}
            />
          </Field>
        </div>
      ) : null}
      {set.DemoPasswordSet ? (
        <Checkbox checked={remove} onChange={setRemove}>
          {t.answers.removeDemoLogin}
        </Checkbox>
      ) : null}
    </FormShell>
  )
}

function ReviewNotesForm({ context, answers }: FormProps) {
  const t = useP2()
  const [notes, setNotes] = useState(readReviewNotes(answers).Notes ?? '')
  const save = useSaveAnswers(context, SavedAnswerKind.ReviewNotes)
  return (
    <FormShell editor={context.editor} loading={save.isPending} onSubmit={() => save.mutate({ answers: { Notes: notes.trim() || null } })}>
      <Field label={t.answers.notes} counter={{ value: notes.length, max: 4000 }}>
        <Textarea rows={6} value={notes} onChange={(event) => setNotes(event.target.value)} />
      </Field>
    </FormShell>
  )
}

function CopyDialog({ context, set, onClose }: { context: WorkspaceContext; set: SavedAnswerSetDto; onClose: () => void }) {
  const t = useP2()
  const queryClient = useQueryClient()
  const { organizationId, app } = context
  const apps = useQuery({
    queryKey: queryKeys.apps.list(organizationId, APP_NAMES_REQUEST),
    queryFn: () => appsApi.list(organizationId, APP_NAMES_REQUEST),
  })
  const others = (apps.data?.Data ?? []).filter((item) => item.Id !== app.Id)
  const [source, setSource] = useState<number | null>(null)
  const [keys, setKeys] = useState<SavedAnswerKind[]>([])
  const filled = new Set((set.Sections ?? []).map((section) => section.Key))
  const copy = useMutation({
    mutationFn: () => answersApi.copy(organizationId, source ?? 0, app.Id, keys),
    onSuccess: () => {
      showToast(t.answers.copied, 'success')
      void queryClient.invalidateQueries({ queryKey: contentKeys.answers(organizationId, app.Id) })
      onClose()
    },
  })
  return (
    <Modal
      open
      onClose={onClose}
      title={t.answers.copyTitle}
      description={interpolate(t.answers.copyBody, { name: app.Name })}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            {t.plan.close}
          </Button>
          <Button variant="primary" disabled={!source || keys.length === 0} loading={copy.isPending} onClick={() => copy.mutate()}>
            {interpolate(t.answers.copyAction, { count: keys.length })}
          </Button>
        </>
      }
    >
      {apps.isLoading ? (
        <CardSkeleton count={1} />
      ) : others.length === 0 ? (
        <p className="text-[13px] text-fg3">{t.answers.noOtherApp}</p>
      ) : (
        <div className="grid gap-3">
          <Field label={t.answers.source}>
            <Select value={source} searchable options={others.map((item) => ({ value: item.Id, label: item.Name }))} onChange={setSource} />
          </Field>
          <fieldset className="grid gap-2">
            <legend className="mb-1 text-[13px] font-semibold">{t.answers.sections}</legend>
            {ANSWER_KINDS.map((kind) => (
              <div key={kind} className="flex items-center justify-between gap-2">
                <Checkbox
                  checked={keys.includes(kind)}
                  onChange={(checked) => setKeys((list) => (checked ? [...list, kind] : list.filter((item) => item !== kind)))}
                >
                  {answerKindLabel(kind, t)}
                </Checkbox>
                <span className="text-xs text-fg3">{filled.has(kind) ? t.answers.overwrite : t.answers.adds}</span>
              </div>
            ))}
          </fieldset>
          <p className="m-0 text-xs text-fg3">{t.answers.noPassword}</p>
        </div>
      )}
    </Modal>
  )
}

const KIND_SLUGS: Record<SavedAnswerKind, string> = {
  [SavedAnswerKind.PrivacyLabels]: 'privacy',
  [SavedAnswerKind.DataSafety]: 'data-safety',
  [SavedAnswerKind.ContentRating]: 'content-rating',
  [SavedAnswerKind.TargetAudience]: 'target-audience',
  [SavedAnswerKind.ReviewLogin]: 'review-login',
  [SavedAnswerKind.ReviewNotes]: 'review-notes',
}

function Answers({ context }: { context: WorkspaceContext }) {
  const t = useP2()
  const format = useFormat()
  const { organizationId, app, editor } = context
  const [kind, setKind] = useState<SavedAnswerKind>(SavedAnswerKind.PrivacyLabels)
  const [copying, setCopying] = useState(false)
  const answers = useQuery({ queryKey: contentKeys.answers(organizationId, app.Id), queryFn: () => answersApi.get(organizationId, app.Id) })
  if (answers.isLoading) {
    return <CardSkeleton />
  }
  if (answers.isError || !answers.data) {
    return <ScreenState error={answers.error} onRetry={() => void answers.refetch()} />
  }
  const set = answers.data
  const section = (set.Sections ?? []).find((item) => item.Key === kind)
  const props: FormProps = { context, set, answers: section?.Answers }
  const forms: Record<SavedAnswerKind, ReactNode> = {
    [SavedAnswerKind.PrivacyLabels]: <PrivacyForm {...props} />,
    [SavedAnswerKind.DataSafety]: <DataSafetyForm {...props} />,
    [SavedAnswerKind.ContentRating]: <ContentRatingForm {...props} />,
    [SavedAnswerKind.TargetAudience]: <TargetAudienceForm {...props} />,
    [SavedAnswerKind.ReviewLogin]: <ReviewLoginForm {...props} />,
    [SavedAnswerKind.ReviewNotes]: <ReviewNotesForm {...props} />,
  }
  return (
    <>
      <PageHeader
        title={t.answers.title}
        subtitle={t.answers.subtitle}
        actions={
          editor ? (
            <Button onClick={() => setCopying(true)}>
              <Copy aria-hidden />
              {t.answers.copy}
            </Button>
          ) : undefined
        }
      />
      <Tabs
        label={t.answers.title}
        value={KIND_SLUGS[kind]}
        onChange={(slug) => setKind(ANSWER_KINDS.find((item) => KIND_SLUGS[item] === slug) ?? SavedAnswerKind.PrivacyLabels)}
        items={ANSWER_KINDS.map((item) => ({ value: KIND_SLUGS[item], label: answerKindLabel(item, t) }))}
      />
      <section className="card mt-3" aria-label={answerKindLabel(kind, t)}>
        <div className="card-h">
          <h2 className="h3">{answerKindLabel(kind, t)}</h2>
          <span className="ml-auto text-xs text-fg3">
            {section?.UpdatedDate
              ? interpolate(t.answers.updated, { name: section.UpdatedByUserName ?? '-', date: format.date(section.UpdatedDate) })
              : t.answers.never}
          </span>
        </div>
        <div className="card-b" key={`${kind}-${section?.UpdatedDate ?? ''}`}>
          {forms[kind]}
        </div>
      </section>
      {copying ? <CopyDialog context={context} set={set} onClose={() => setCopying(false)} /> : null}
    </>
  )
}

export function AnswersPage() {
  return (
    <AppWorkspace section="answers" appWide>
      {(context) => <Answers context={context} />}
    </AppWorkspace>
  )
}
