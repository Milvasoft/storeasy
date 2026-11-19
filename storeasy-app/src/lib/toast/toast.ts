// A framework-agnostic toast store, so React components and plain modules (the query client) can raise toasts alike.
// Design 18.20: bottom right, at most 4, success 4 s, error 8 s, the same message is not shown twice in a row.
export type ToastType = 'success' | 'error' | 'info' | 'warning'

// One button on the toast (an undo); pressing it runs onClick and closes the toast.
export interface ToastAction {
  label: string
  onClick: () => void
}

export interface ToastItem {
  id: number
  message: string
  // A second line under the message (an address, a hint).
  detail?: string
  type: ToastType
  action?: ToastAction
}

// A toast with a button stays a little longer, so there is time to reach it.
const ACTION_EXTRA_MS = 2000

const DURATION_MS: Record<ToastType, number> = { success: 4000, info: 4000, warning: 6000, error: 8000 }
const MAX_TOASTS = 4

let items: ToastItem[] = []
let sequence = 0
const listeners = new Set<() => void>()

function emit(): void {
  for (const listener of listeners) {
    listener()
  }
}

export function subscribeToasts(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getToasts(): ToastItem[] {
  return items
}

export function dismissToast(id: number): void {
  items = items.filter((item) => item.id !== id)
  emit()
}

export function showToast(message: string, type: ToastType = 'info', detail?: string, action?: ToastAction): void {
  const text = message.trim()
  if (!text) {
    return
  }
  // The same toast raised back to back shows once.
  const last = items[items.length - 1]
  if (last && last.message === text && last.type === type) {
    return
  }
  sequence += 1
  const id = sequence
  // The fifth toast pushes the oldest one out.
  items = [...items, { id, message: text, type, detail, action }].slice(-MAX_TOASTS)
  emit()
  window.setTimeout(() => dismissToast(id), DURATION_MS[type] + (action ? ACTION_EXTRA_MS : 0))
}
