import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ExternalLink } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { PageHeader } from '@/components/ui/primitives'
import { useFormat } from '@/lib/i18n/I18nContext'
import { useMs } from './i18n/useMs'
import { reasonLabel } from './labels'
import { MsSubmitDialog, MsWorkspace, SubmitButton, type MsContext, type SubmitTarget } from './MsWorkspace'
import { SubmissionCard } from './SubmissionsPage'
import { MsProductKind, MsSubmissionTarget } from './types'

// The product screen: what the store says about the product (flags), its active submission, and the assisted steps
// Microsoft has no API for, each with its Partner Center page and its step in storeasy.

function Flag({ label, on }: { label: string; on: boolean }) {
  const t = useMs()
  return (
    <>
      <dt>{label}</dt>
      <dd>
        <Badge small tone={on ? 'accent' : 'neutral'}>
          {on ? t.product.yes : t.product.no}
        </Badge>
      </dd>
    </>
  )
}

function Product({ context }: { context: MsContext }) {
  const t = useMs()
  const format = useFormat()
  const { product, organizationId, app } = context
  const [target, setTarget] = useState<SubmitTarget | null>(null)
  const steps = product.AssistedSteps ?? []
  const msiExe = product.Kind === MsProductKind.MsiExe
  return (
    <>
      <PageHeader
        title={product.Name ?? t.product.title}
        subtitle={msiExe ? t.kind.msiExe : t.kind.msix}
        actions={
          msiExe && product.IsReady ? (
            <SubmitButton
              context={context}
              label={t.product.submit}
              onClick={() => setTarget({ Target: MsSubmissionTarget.Installer, label: product.ProductId ?? '-' })}
            />
          ) : undefined
        }
      />
      <div className="grid gap-4 lg:grid-cols-2">
        <section className="card" aria-label={t.product.flags}>
          <div className="card-h">
            <h2 className="h3">{t.product.flags}</h2>
          </div>
          <dl className="card-b kv text-[13px]">
            <dt>{t.product.storeId}</dt>
            <dd className="mono">{product.ProductId ?? '-'}</dd>
            {product.PackageFamilyName ? (
              <>
                <dt>{t.product.packageFamily}</dt>
                <dd className="mono break-all">{product.PackageFamilyName}</dd>
              </>
            ) : null}
            <dt>{t.product.synced}</dt>
            <dd>{product.SyncedDate ? format.dateTime(product.SyncedDate) : t.product.notSynced}</dd>
            <Flag label={t.product.pricingV2} on={product.PricingV2} />
            {msiExe ? (
              <Flag label={t.product.ready} on={product.IsReady} />
            ) : (
              <>
                <Flag label={t.product.firstSubmission} on={product.FirstSubmissionDone} />
                <Flag label={t.product.mandatoryUpdate} on={product.HasMandatoryUpdate} />
              </>
            )}
            <dt>{t.product.lastPublished}</dt>
            <dd className="mono">{product.LastPublishedSubmissionId ?? '-'}</dd>
            <dt>{t.product.pending}</dt>
            <dd className="mono">{product.PendingSubmissionId ?? '-'}</dd>
          </dl>
        </section>
        <section className="grid content-start gap-2" aria-label={t.product.active}>
          <h2 className="h3">{t.product.active}</h2>
          {product.ActiveSubmission ? (
            <SubmissionCard submission={product.ActiveSubmission} context={context} onSubmit={setTarget} />
          ) : (
            <p className="m-0 text-[13px] text-fg3">{t.product.noActive}</p>
          )}
        </section>
      </div>
      <section className="card mt-4" aria-label={t.product.assisted}>
        <div className="card-h">
          <h2 className="h3">{t.product.assisted}</h2>
        </div>
        <div className="card-b grid gap-3">
          <p className="m-0 text-[13px] text-fg2">{t.product.assistedLead}</p>
          {steps.length === 0 ? <p className="m-0 text-[13px] text-fg3">{t.product.noSteps}</p> : null}
          <ul className="m-0 grid list-none gap-2 p-0">
            {steps.map((step) => (
              <li key={step.Key} className="flex flex-wrap items-center gap-2 rounded-md border border-border p-3 text-[13px]">
                <b className="min-w-0 flex-1">{step.Title ?? step.Key}</b>
                {step.Reason ? <Badge small>{reasonLabel(step.Reason, t)}</Badge> : null}
                <Badge small tone={step.Pending ? 'warning' : 'success'}>
                  {step.Pending ? t.product.stepPending : t.product.stepDone}
                </Badge>
                {step.Url ? (
                  <a className="lnk inline-flex items-center gap-1" href={step.Url} target="_blank" rel="noreferrer">
                    <ExternalLink className="h-3.5 w-3.5" aria-hidden />
                    {t.product.openPartnerCenter}
                  </a>
                ) : null}
                {step.StepId ? (
                  <Link className="lnk" to={`/apps/${app.Id}/assisted/${step.StepId}`}>
                    {t.product.openStep}
                  </Link>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      </section>
      <MsSubmitDialog target={target} onClose={() => setTarget(null)} organizationId={organizationId} product={product} />
    </>
  )
}

export function MsProductPage() {
  return <MsWorkspace sub="product">{(context) => <Product context={context} />}</MsWorkspace>
}
