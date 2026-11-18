import type { ReactNode } from 'react'
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher'
import { Menu } from '@/components/ui/Menu'
import { SOURCE_URL } from '@/config'
import { usePrefersReducedMotion } from '@/lib/hooks/usePrefersReducedMotion'
import { useT } from '@/lib/i18n/I18nContext'
import { Brand, LOGO_PATHS } from './Logo'

// The sign-in illustration (design 5.1): storeasy's "Hat", three stores on one line. Packets flow only while motion is
// allowed; with reduced motion the rails and still packets stay.
function HatArt() {
  const t = useT()
  const reduced = usePrefersReducedMotion()
  return (
    <div className="auth-art" aria-hidden>
      {/* meet, not slice: on wide or short panels slice scaled the drawing past the panel and cut the headline off. */}
      <svg viewBox="0 0 720 820" preserveAspectRatio="xMidYMid meet">
        <g fill="none" stroke="rgb(var(--border-strong))" strokeWidth="2" strokeDasharray="2 8" strokeLinecap="round">
          <path d="M-40 518 H250" />
          <path d="M346 518 C 410 518, 400 346, 470 346" />
          <path d="M346 518 H470" />
          <path d="M346 518 C 410 518, 400 690, 470 690" />
        </g>
        <text x="250" y="168" className="disp" fontWeight="800" fontSize="56" letterSpacing="-2" fill="rgb(var(--fg))">
          {t.auth.artLine1}
        </text>
        <text x="250" y="228" className="disp" fontWeight="800" fontSize="56" letterSpacing="-2" fill="rgb(var(--accent-text))">
          {t.auth.artLine2}
        </text>
        <text x="252" y="268" fontSize="15" fill="rgb(var(--fg-2))">
          {t.auth.artCaption}
        </text>
        <rect x="250" y="470" width="96" height="96" rx="26" fill="rgb(var(--accent))" />
        <g transform="translate(268 488) scale(1.25)">
          {LOGO_PATHS.map((path, index) => (
            <path key={path.d} d={path.d} fill="rgb(var(--accent-fg))" opacity={index === 1 ? 0.55 : 1} />
          ))}
        </g>
        <g className="mono" fontSize="11" letterSpacing="1">
          <rect x="470" y="300" width="220" height="92" rx="20" fill="rgb(var(--bg))" stroke="rgb(var(--border-strong))" />
          <text x="490" y="334" fill="rgb(var(--fg-3))">
            {t.auth.artStoreApple}
          </text>
          <rect x="470" y="472" width="220" height="92" rx="20" fill="rgb(var(--bg))" stroke="rgb(var(--border-strong))" />
          <text x="490" y="506" fill="rgb(var(--fg-3))">
            {t.auth.artStoreGoogle}
          </text>
          <rect x="470" y="644" width="220" height="92" rx="20" fill="rgb(var(--bg))" stroke="rgb(var(--border-strong))" />
          <text x="490" y="678" fill="rgb(var(--fg-3))">
            {t.auth.artStoreMicrosoft}
          </text>
        </g>
        <g className="disp" fontWeight="700" fontSize="18" fill="rgb(var(--fg))">
          <text x="490" y="364">
            {t.auth.artApple}
          </text>
          <text x="490" y="536">
            {t.auth.artGoogle}
          </text>
          <text x="490" y="708">
            {t.auth.artMicrosoft}
          </text>
        </g>
        <text x="252" y="790" className="mono" fontSize="11" fill="rgb(var(--fg-3))">
          {t.auth.artFooter}
        </text>
        {reduced ? (
          <g fill="rgb(var(--accent))">
            <circle cx="420" cy="380" r="6" />
            <circle cx="410" cy="518" r="6" />
            <circle cx="430" cy="670" r="6" />
          </g>
        ) : (
          <g fill="rgb(var(--accent))">
            <circle r="6">
              <animateMotion dur="3.2s" repeatCount="indefinite" path="M346 518 C 410 518, 400 346, 470 346" />
            </circle>
            <circle r="6">
              <animateMotion dur="2.6s" begin="-1s" repeatCount="indefinite" path="M346 518 H470" />
            </circle>
            <circle r="6">
              <animateMotion
                dur="3.6s"
                begin="-2s"
                repeatCount="indefinite"
                keyPoints="1;0"
                keyTimes="0;1"
                calcMode="linear"
                path="M346 518 C 410 518, 400 690, 470 690"
              />
            </circle>
            <circle r="5" opacity=".6">
              <animateMotion dur="4s" begin="-.5s" repeatCount="indefinite" path="M-40 518 H250" />
            </circle>
          </g>
        )}
      </svg>
    </div>
  )
}

// Design 5.34: no cookie banner (only required storage); the footer link opens what this site keeps.
function StorageNote() {
  const t = useT()
  return (
    <Menu
      label={t.storage.title}
      placement="top-start"
      className="w-[min(340px,calc(100vw-32px))]"
      trigger={
        <button type="button" className="underline decoration-fg3/40 underline-offset-2 hover:text-fg">
          {t.storage.link}
        </button>
      }
      entries={[
        { key: 'heading', heading: t.storage.title },
        {
          key: 'items',
          content: (
            <dl className="grid gap-2 px-2.5 pb-2 text-[12.5px]">
              {t.storage.items.map((item) => (
                <div key={item.name}>
                  <dt className="font-semibold text-fg">{item.name}</dt>
                  <dd className="m-0 text-fg2">{item.description}</dd>
                </div>
              ))}
              <p className="m-0 text-fg3">{t.storage.noTracking}</p>
            </dl>
          ),
        },
      ]}
    />
  )
}

export function AuthFooter() {
  const t = useT()
  return (
    <footer className="auth-foot">
      <span className="flex flex-wrap items-center gap-1">
        {t.footer.license} ·{' '}
        <a
          href={SOURCE_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="underline decoration-fg3/40 underline-offset-2 hover:text-fg"
        >
          {t.footer.sourceCode}
        </a>{' '}
        · <StorageNote />
      </span>
      <LanguageSwitcher />
    </footer>
  )
}

/** The sign-in family of pages: the form on the left, the Hat on the right (hidden below 1024 px, design 5.35). */
export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="auth">
      <div className="auth-f">
        <main className="auth-c">{children}</main>
        <AuthFooter />
      </div>
      <HatArt />
    </div>
  )
}

/** A single centered card (verify e-mail, reset password, invitation, no access): the brand on top, the footer below. */
export function SoloLayout({ children, wide }: { children: ReactNode; wide?: boolean }) {
  return (
    <div className="solo">
      <Brand className="!p-0" />
      <main className={wide ? 'solo-c !max-w-[1000px]' : 'solo-c'}>{children}</main>
      <AuthFooter />
    </div>
  )
}

/** The logo, title and lead line at the top of an auth form. */
export function AuthLead({ title, lead }: { title: string; lead?: ReactNode }) {
  return (
    <div className="mb-1 grid justify-items-center gap-3 text-center">
      <Brand className="!p-0" />
      <h1>{title}</h1>
      {lead ? <p className="m-0 text-sm text-fg2">{lead}</p> : null}
    </div>
  )
}

/** The card of a single-purpose auth screen: an icon tile, the title, the body (design 5.14-5.25). */
export function AuthCard({
  icon,
  tone,
  title,
  children,
}: {
  icon: ReactNode
  tone?: 'ok' | 'warn' | 'dng'
  title: string
  children: ReactNode
}) {
  return (
    <div className="card grid gap-4 p-6 text-center sm:p-8">
      <div className={tone ? `ico-c ${tone}` : 'ico-c'}>{icon}</div>
      <h1 className="h2">{title}</h1>
      {children}
    </div>
  )
}
