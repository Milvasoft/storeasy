import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Menu as MenuIcon } from 'lucide-react'
import { ThemeMenuButton } from '@/components/common/ThemeSwitcher'
import { Banner } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Button, IconButton } from '@/components/ui/primitives'
import { TopProgress } from '@/components/ui/TopProgress'
import { useActivity } from '@/lib/activity/ActivityContext'
import { activeJobCount } from '@/lib/activity/hatState'
import { useOnlineStatus } from '@/lib/hooks/useOnlineStatus'
import { useT } from '@/lib/i18n/I18nContext'
import { interpolate } from '@/lib/i18n/interpolate'
import { navItemFor, navLabel } from '@/lib/nav/nav'
import { useOrganization } from '@/lib/org/OrganizationContext'
import { showToast } from '@/lib/toast/toast'
import { NotificationBell } from './NotificationBell'
import { Sidebar } from './Sidebar'

function Breadcrumb() {
  const t = useT()
  const { pathname } = useLocation()
  const { current } = useOrganization()
  const item = navItemFor(pathname)
  const last = item ? navLabel(item.key, t) : pathname.startsWith('/account') ? t.account.title : t.states.notFoundShort
  return (
    <nav className="crumb" aria-label={t.nav.breadcrumb}>
      <span className="max-lg:hidden">{current?.Name}</span>
      <span className="sep max-lg:hidden" aria-hidden>
        /
      </span>
      <b aria-current="page">{last}</b>
    </nav>
  )
}

function LivePill() {
  const t = useT()
  const { state, connection } = useActivity()
  const count = activeJobCount(state)
  if (connection.state !== 'connected' || count === 0) {
    return null
  }
  return (
    <span className="livepill">
      <span className="dot" aria-hidden />
      <span className="max-sm:hidden">{interpolate(t.activity.runningJobs, { count })}</span>
      <span className="sm:hidden">{count}</span>
    </span>
  )
}

// Design 18.11: a strip above the content while offline; one info toast when the connection is back.
function OfflineBanner() {
  const t = useT()
  const online = useOnlineStatus()
  const queryClient = useQueryClient()
  const wasOffline = useRef(false)

  useEffect(() => {
    if (!online) {
      wasOffline.current = true
    } else if (wasOffline.current) {
      wasOffline.current = false
      showToast(t.states.backOnline, 'info')
      void queryClient.refetchQueries({ type: 'active' })
    }
  }, [online, queryClient, t.states.backOnline])

  if (online) {
    return null
  }
  return (
    <Banner
      tone="danger"
      role="alert"
      action={
        <Button size="sm" variant="inverse" onClick={() => void queryClient.refetchQueries({ type: 'active' })}>
          {t.states.retry}
        </Button>
      }
    >
      <b>{t.states.offlineTitle}</b> <span className="text-fg2">{t.states.offlineBanner}</span>
    </Banner>
  )
}

/**
 * The app shell (design 19 section 7): a 248 px menu on the left from 1024 px, a drawer below; a 60 px top bar with the
 * breadcrumb, the live pill, the theme menu and the bell. The page owns everything under the bar.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const t = useT()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { pathname } = useLocation()

  useEffect(() => {
    setDrawerOpen(false)
  }, [pathname])

  return (
    <div className="flex min-h-full">
      <aside className="side sticky top-0 h-screen max-lg:!hidden" aria-label={t.nav.label}>
        <Sidebar />
      </aside>
      <Modal
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        variant="drawer"
        side="left"
        bare
        label={t.nav.label}
        panelClassName="side !w-[300px] max-w-[85vw]"
      >
        <Sidebar onNavigate={() => setDrawerOpen(false)} />
      </Modal>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="bar">
          <IconButton label={t.nav.openMenu} ghost className="lg:!hidden" onClick={() => setDrawerOpen(true)}>
            <MenuIcon aria-hidden />
          </IconButton>
          <Breadcrumb />
          <span className="ml-auto flex items-center gap-1.5 sm:gap-2.5">
            <LivePill />
            <ThemeMenuButton />
            <NotificationBell />
          </span>
        </header>
        <OfflineBanner />
        <main id="main" className="content relative">
          <TopProgress />
          {children}
        </main>
      </div>
    </div>
  )
}
