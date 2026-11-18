import { cn } from '@/lib/utils/cn'

// The K3 "four wing e" mark (design 19 section 1): three half-disc wings, the missing one is the mouth of the "e".
// Two tones from the .mk class (--m1, --m2); .inv on an accent background, .mono-m for one color.
export const LOGO_PATHS = [
  { d: 'M24 24H43a9.5 9.5 0 0 0-19 0z', tone: 'var(--m1)' },
  { d: 'M24 24V5a9.5 9.5 0 0 0 0 19z', tone: 'var(--m2)' },
  { d: 'M24 24H5a9.5 9.5 0 0 0 19 0z', tone: 'var(--m1)' },
] as const

export function LogoMark({
  size = 28,
  inverse,
  spinning,
  className,
}: {
  size?: number
  inverse?: boolean
  spinning?: boolean
  className?: string
}) {
  return (
    <svg
      className={cn('mk', inverse && 'inv', spinning && 'spin-mk', className)}
      width={size}
      height={size}
      viewBox="0 0 48 48"
      aria-hidden
    >
      {LOGO_PATHS.map((path) => (
        <path key={path.d} d={path.d} fill={path.tone} />
      ))}
    </svg>
  )
}

// The word mark: lower case "storeasy", Bricolage 800, the middle "e" in the accent. The name is not translated.
export function Wordmark() {
  return (
    <span className="wm" aria-label="storeasy">
      stor<i>e</i>asy
    </span>
  )
}

export function Brand({ className }: { className?: string }) {
  return (
    <span className={cn('brand', className)}>
      <LogoMark />
      <Wordmark />
    </span>
  )
}
