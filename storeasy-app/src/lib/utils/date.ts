import type { DateOnly } from '@/lib/api/types'

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/

// A calendar day ("2026-11-24") is that day wherever the viewer is: built in local time, because new Date('2026-11-24')
// is UTC midnight and shows the day before west of Greenwich.
export function parseDateOnly(value?: DateOnly | null): Date | null {
  const match = value ? DATE_ONLY.exec(value) : null
  if (!match) {
    return null
  }
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  // 2027-02-31 rolls over into March: such a day does not exist.
  if (date.getMonth() !== Number(match[2]) - 1) {
    return null
  }
  return date
}

// The local calendar day of a Date as "yyyy-MM-dd". Not toISOString().slice(0, 10), which is the UTC day.
export function toDateOnly(date: Date): DateOnly {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function todayDateOnly(): DateOnly {
  return toDateOnly(new Date())
}

export function addDays(day: DateOnly, count: number): DateOnly {
  const date = parseDateOnly(day) ?? new Date()
  date.setDate(date.getDate() + count)
  return toDateOnly(date)
}

// The first and the last moment of a local day as UTC ISO strings, for a Between filter (design 19.6: the day boundaries
// are converted to UTC; a DateOnly field is not).
export function dayStartIso(day: DateOnly): string {
  return (parseDateOnly(day) ?? new Date()).toISOString()
}

export function dayEndIso(day: DateOnly): string {
  const date = parseDateOnly(day) ?? new Date()
  date.setDate(date.getDate() + 1)
  date.setMilliseconds(-1)
  return date.toISOString()
}
