import { useState } from 'react'
import { FileKey } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { FileDrop } from '@/components/ui/FileDrop'
import { SegmentedControl } from '@/components/ui/Tabs'
import { Button, Field, Input, Textarea } from '@/components/ui/primitives'
import { TEXT_LIMITS } from '@/lib/api/textLimits'
import { StoreKind } from '@/lib/api/types'
import { readServiceAccount } from '@/lib/connections/secrets'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'

// The key pieces of the connection forms (design 14.5-14.9), shared by adding a connection and replacing its key: the
// Apple issuer and key id fields, and the .p8 or service account JSON given as a file or pasted text.

type Source = 'file' | 'paste'

/** The key text and, when it came from a file, the file's name. */
export interface KeyText {
  text: string
  fileName: string | null
}

export const EMPTY_KEY: KeyText = { text: '', fileName: null }

/** The Issuer ID and Key ID of an App Store Connect key. */
export function AppleKeyFields({
  issuerId,
  keyId,
  onIssuerId,
  onKeyId,
  errors,
  issuerHint,
}: {
  issuerId: string
  keyId: string
  onIssuerId: (value: string) => void
  onKeyId: (value: string) => void
  errors: { issuerId?: string | null; keyId?: string | null }
  issuerHint?: string
}) {
  const t = useT()
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <Field label={t.connections.issuerId} error={errors.issuerId} hint={issuerHint ?? t.connections.issuerHint}>
        <Input mono value={issuerId} spellCheck={false} autoComplete="off" onChange={(event) => onIssuerId(event.target.value)} />
      </Field>
      <Field label={t.connections.keyId} error={errors.keyId} hint={t.connections.keyIdHint}>
        <Input
          mono
          value={keyId}
          spellCheck={false}
          autoComplete="off"
          maxLength={10}
          onChange={(event) => onKeyId(event.target.value.toUpperCase())}
        />
      </Field>
    </div>
  )
}

/**
 * The .p8 key (Apple) or the service account JSON (Google), chosen as a file or pasted, with the error under it and, for a
 * Google key, the service account e-mail and project read from it. The key text is never put on screen once read from a
 * file. onFile hears the file name (an Apple key id is read from it).
 */
export function ConnectionKeyInput({
  store,
  value,
  onChange,
  onFile,
  onFileRejected,
  error,
}: {
  store: StoreKind
  value: KeyText
  onChange: (value: KeyText) => void
  onFile?: (fileName: string) => void
  onFileRejected: (message: string) => void
  error?: string | null
}) {
  const t = useT()
  const [source, setSource] = useState<Source>('file')
  const apple = store === StoreKind.Apple
  const label = apple ? t.connections.p8 : t.connections.json.label
  const google = !apple && value.text ? readServiceAccount(value.text).info : null

  const takeFile = async (file: File) => {
    const text = await file.text()
    onChange({ text, fileName: file.name })
    onFile?.(file.name)
  }

  return (
    <>
      <div className="grid gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="lbl">{label}</span>
          <span className="ml-auto">
            <SegmentedControl
              label={t.connections.sourceLabel}
              value={source}
              onChange={(next) => {
                setSource(next)
                onChange({ text: '', fileName: null })
              }}
              options={[
                { value: 'file', label: t.connections.sourceFile },
                { value: 'paste', label: t.connections.sourcePaste },
              ]}
            />
          </span>
        </div>
        {source === 'file' ? (
          value.fileName ? (
            <div className="secret">
              <FileKey className="h-4 w-4 flex-none text-fg3" aria-hidden />
              <span className="min-w-0 flex-1 truncate">{value.fileName}</span>
              <Badge tone="success" small>
                {t.connections.fileRead}
              </Badge>
              <Button size="sm" variant="ghost" onClick={() => onChange({ text: '', fileName: null })}>
                {t.connections.changeFile}
              </Button>
            </div>
          ) : (
            <FileDrop
              label={label}
              accept={apple ? ['.p8'] : ['.json']}
              maxBytes={TEXT_LIMITS.connectionSecret}
              hint={apple ? t.connections.p8Hint : t.connections.json.hint}
              onFile={(file) => void takeFile(file)}
              onReject={(file, reason) =>
                onFileRejected(
                  interpolate(reason === 'type' ? t.connections.fileWrongType : t.connections.fileTooLarge, {
                    name: file.name,
                  }),
                )
              }
            />
          )
        ) : (
          <Textarea
            mono
            rows={6}
            aria-label={label}
            value={value.text}
            maxLength={TEXT_LIMITS.connectionSecret}
            spellCheck={false}
            autoComplete="off"
            placeholder={apple ? '-----BEGIN PRIVATE KEY-----' : '{ "type": "service_account", ... }'}
            onChange={(event) => onChange({ text: event.target.value, fileName: null })}
          />
        )}
        {error ? <span className="err-t">{error}</span> : null}
      </div>
      {google && (google.clientEmail || google.projectId) ? (
        <dl className="kv rounded-md border border-border p-3">
          <dt>{t.connections.serviceAccount}</dt>
          <dd className="mono">{google.clientEmail ?? '-'}</dd>
          <dt>{t.connections.project}</dt>
          <dd className="mono">{google.projectId ?? '-'}</dd>
        </dl>
      ) : null}
    </>
  )
}
