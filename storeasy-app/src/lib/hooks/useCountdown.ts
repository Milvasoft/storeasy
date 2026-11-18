import { useCallback, useEffect, useState } from 'react'

/**
 * A seconds countdown for Retry-After (429) and resend buttons: start(n) and the value ticks down to 0 once a second.
 * The caller keeps its button disabled while the value is above 0.
 */
export function useCountdown(): { seconds: number; start: (seconds: number) => void } {
  const [seconds, setSeconds] = useState(0)

  useEffect(() => {
    if (seconds <= 0) {
      return
    }
    const timer = window.setTimeout(() => setSeconds((value) => Math.max(0, value - 1)), 1000)
    return () => window.clearTimeout(timer)
  }, [seconds])

  const start = useCallback((value: number) => setSeconds(Math.max(0, Math.ceil(value))), [])

  return { seconds, start }
}

// "0:42".
export function formatCountdown(seconds: number): string {
  const minutes = Math.floor(seconds / 60)
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`
}
