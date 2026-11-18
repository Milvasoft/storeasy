import { createContext, useContext, useMemo, type ReactNode } from 'react'

// Sections of the design whose API does not exist yet. Each one is OFF until its endpoints ship; while off, the route
// renders the section's designed empty state with a "coming" mark (ComingSection) instead of a half-working screen.
// There is no anonymous flag endpoint for app users yet (02 section 10.1 GET Public/feature-flags), so the values live
// here, in code; when the API gains one, this provider reads it and these become the fallback.
export type FeatureFlagKey =
  | 'Compliance'
  | 'Reviews'
  | 'Studio'
  // The studio's AI caption suggestions (server kill switch Studio.AiCaptions, 503 when off).
  | 'StudioAiCaptions'
  | 'Intake'
  | 'Webhooks'
  | 'Rules'
  | 'Developer'
  | 'Notifications'
  | 'Plans'
  | 'OrganizationRetention'
  | 'AccountSessions'
  | 'Platform'
  | 'RuleWatcher'

export type FeatureFlags = Record<FeatureFlagKey, boolean>

export const DEFAULT_FLAGS: FeatureFlags = {
  Compliance: true,
  Reviews: true,
  Studio: true,
  StudioAiCaptions: false,
  Intake: true,
  Webhooks: true,
  Rules: true,
  Developer: true,
  Notifications: true,
  Plans: true,
  OrganizationRetention: false,
  AccountSessions: false,
  Platform: false,
  // The rule watcher's proposals and sources (platform administrators, phase 4); the other platform screens wait.
  RuleWatcher: true,
}

interface FeatureFlagsValue {
  flags: FeatureFlags
  isEnabled: (key: FeatureFlagKey) => boolean
}

const FeatureFlagsContext = createContext<FeatureFlagsValue | null>(null)

// `flags` lets a test (or a later server source) switch sections on.
export function FeatureFlagsProvider({ children, flags }: { children: ReactNode; flags?: Partial<FeatureFlags> }) {
  const value = useMemo<FeatureFlagsValue>(() => {
    const merged = { ...DEFAULT_FLAGS, ...flags }
    return { flags: merged, isEnabled: (key) => merged[key] }
  }, [flags])
  return <FeatureFlagsContext.Provider value={value}>{children}</FeatureFlagsContext.Provider>
}

export function useFeature(key: FeatureFlagKey): boolean {
  const ctx = useContext(FeatureFlagsContext)
  if (!ctx) {
    throw new Error('useFeature must be used within FeatureFlagsProvider')
  }
  return ctx.isEnabled(key)
}
