// The guided tour's steps, in order. Each step may open a page first and points at the UI through data-tour anchors:
// the first anchor that is on screen gets the spotlight. An anchor that exists but is hidden (the menu on a phone) shows
// the card in the middle of the screen; a step none of whose anchors exist (no permission, a section that is off) is
// skipped. A step whose page needs an app is left out while the organization has none.

export type TourStepId =
  | 'welcome'
  | 'nav'
  | 'apps'
  | 'topbar'
  | 'app'
  | 'appTabs'
  | 'mute'
  | 'plans'
  | 'live'
  | 'reviews'
  | 'rejections'
  | 'compliance'
  | 'intake'
  | 'members'
  | 'audit'
  | 'developer'
  | 'restart'
  | 'done'

export interface TourContextData {
  // The first app of the organization, for the app pages; null while it has none.
  appId: number | null
}

export interface TourStep {
  id: TourStepId
  // The page to open first: a path, or a function of the context that answers null when the step cannot be shown.
  path?: string | ((context: TourContextData) => string | null)
  // data-tour anchors in order of preference. None: the card stands in the middle of the screen.
  targets?: string[]
}

// A resolved step: its page known, ready to run.
export interface TourStop {
  id: TourStepId
  path: string | null
  targets: string[]
}

const appPath = (suffix: string) => (context: TourContextData) => (context.appId ? `/apps/${context.appId}${suffix}` : null)

export const TOUR_STEPS: TourStep[] = [
  { id: 'welcome', path: '/apps' },
  { id: 'nav', path: '/apps', targets: ['sidebar-nav', 'menu-button'] },
  { id: 'apps', path: '/apps', targets: ['apps', 'nav-apps'] },
  { id: 'topbar', path: '/apps', targets: ['topbar'] },
  { id: 'app', path: appPath(''), targets: ['app-header'] },
  { id: 'appTabs', path: appPath(''), targets: ['app-tabs'] },
  { id: 'mute', path: appPath(''), targets: ['app-mute'] },
  { id: 'plans', path: '/activity/plans', targets: ['plans', 'nav-activity'] },
  { id: 'live', path: '/activity', targets: ['activity-board', 'nav-activity'] },
  { id: 'reviews', path: '/reviews', targets: ['reviews', 'nav-reviews'] },
  { id: 'rejections', path: appPath('/rejections'), targets: ['rejections'] },
  { id: 'compliance', path: '/compliance', targets: ['compliance', 'nav-compliance'] },
  { id: 'intake', path: '/intake', targets: ['intake', 'nav-intake'] },
  { id: 'members', path: '/members', targets: ['members', 'nav-members'] },
  { id: 'audit', path: '/audit', targets: ['audit', 'nav-audit'] },
  { id: 'developer', path: '/developer', targets: ['developer', 'nav-developer'] },
  { id: 'restart', path: '/account', targets: ['account-tour'] },
  { id: 'done' },
]

/** The steps this organization can be shown, with their pages resolved. */
export function resolveSteps(steps: TourStep[], context: TourContextData): TourStop[] {
  return steps.flatMap((step) => {
    const path = typeof step.path === 'function' ? step.path(context) : (step.path ?? null)
    if (typeof step.path === 'function' && path === null) {
      return []
    }
    return [{ id: step.id, path, targets: step.targets ?? [] }]
  })
}

// rank: the place of the found anchor in the list (0 is the preferred one).
export type TargetLookup = { state: 'found'; element: HTMLElement; rank: number } | { state: 'hidden' } | { state: 'missing' }

function onScreen(element: HTMLElement): boolean {
  const rect = element.getBoundingClientRect()
  return rect.width > 0 && rect.height > 0
}

/** The first anchor of the list that is on screen; else whether any of them exists at all. */
export function findTarget(targets: string[], root: ParentNode = document): TargetLookup {
  let exists = false
  for (const [rank, target] of targets.entries()) {
    const elements = Array.from(root.querySelectorAll<HTMLElement>(`[data-tour="${target}"]`))
    exists ||= elements.length > 0
    const visible = elements.find(onScreen)
    if (visible) {
      return { state: 'found', element: visible, rank }
    }
  }
  return exists ? { state: 'hidden' } : { state: 'missing' }
}
