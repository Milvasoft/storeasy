import { useId, useRef, useState, type DragEvent } from 'react'
import { Upload } from 'lucide-react'
import { useT } from '@/lib/i18n/I18nContext'
import { cn } from '@/lib/utils/cn'

/**
 * A drop zone with a "choose from computer" fallback (design 19: file drop). Type and size are checked here, before any
 * request; the drag state is neutral (not the accent). The file is handed to the caller, never uploaded by this component.
 */
export function FileDrop({
  accept,
  maxBytes,
  hint,
  onFile,
  onReject,
  label,
}: {
  // Extensions with the dot (".p8", ".json"); the check is by name because .p8 has no registered media type.
  accept: string[]
  maxBytes: number
  hint: string
  onFile: (file: File) => void
  onReject: (file: File, reason: 'type' | 'size') => void
  label: string
}) {
  const t = useT()
  const inputId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)

  const take = (file: File | undefined) => {
    if (!file) {
      return
    }
    const name = file.name.toLowerCase()
    if (!accept.some((extension) => name.endsWith(extension))) {
      onReject(file, 'type')
      return
    }
    if (file.size > maxBytes) {
      onReject(file, 'size')
      return
    }
    onFile(file)
  }

  const onDrop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    take(event.dataTransfer.files?.[0])
  }

  return (
    <div
      className={cn(
        'flex flex-col items-center gap-2 rounded-md border-[1.5px] border-dashed border-borderControl px-4 py-6 text-center text-[13px] text-fg2 transition-colors duration-1',
        dragging && 'border-fg bg-bg2',
      )}
      onDragOver={(event) => {
        event.preventDefault()
        setDragging(true)
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={onDrop}
    >
      <Upload className="h-5 w-5 text-fg3" aria-hidden />
      <span>
        {t.fileDrop.dropOr}{' '}
        <label htmlFor={inputId} className="lnk">
          {t.fileDrop.choose}
        </label>
      </span>
      <span className="text-xs text-fg3">{hint}</span>
      <input
        ref={inputRef}
        id={inputId}
        type="file"
        className="sr-only"
        aria-label={label}
        accept={accept.join(',')}
        onChange={(event) => {
          take(event.target.files?.[0])
          event.target.value = ''
        }}
      />
    </div>
  )
}
