import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { systemApi } from '@/lib/api/endpoints'
import type { FeatureFlagStateDto } from '@/lib/api/types'

// Sections of the design and the server's kill switches. The client keeps a default for every key (a section whose API
// does not exist yet stays OFF and renders its designed "coming" state); once signed in, GET system/feature-flags answers
// the server's switches and those win over the defaults. A server key is matched without its dots ("Studio.AiCaptions" is
// StudioAiCaptions); keys the client does not know are ignored.
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
  // Organization settings: audit retention and default language (organizations/settings, phase 8).
  OrganizationRetention: true,
  // The session list of account security (account/sessions, phase 8).
  AccountSessions: true,
  // The platform administration screens (users, roles, activity log, languages, feature flags, system status).
  Platform: true,
  // The rule watcher's proposals and sources (platform administrators, phase 4).
  RuleWatcher: true,
}

/** The server's flag states as client keys: known keys only, matched without dots. */
export function serverFlags(states: FeatureFlagStateDto[] | null | undefined): Partial<FeatureFlags> {
  const known = new Set(Object.keys(DEFAULT_FLAGS))
  const result: Partial<FeatureFlags> = {}
  for (const state of states ?? []) {
    const key = state.Key.replace(/\./g, '')
    if (known.has(key)) {
      result[key as FeatureFlagKey] = state.IsEnabled
    }
  }
  return result
}

interface FeatureFlagsValue {
  flags: FeatureFlags
  isEnabled: (key: FeatureFlagKey) => boolean
  setServer: (flags: Partial<FeatureFlags>) => void
}

const FeatureFlagsContext = createContext<FeatureFlagsValue | null>(null)

// `flags` lets a test pin a section on or off; it wins over the server so the test does not depend on the fake backend.
export function FeatureFlagsProvider({ children, flags }: { children: ReactNode; flags?: Partial<FeatureFlags> }) {
  const [server, setServer] = useState<Partial<FeatureFlags>>({})
  const value = useMemo<FeatureFlagsValue>(() => {
    const merged = { ...DEFAULT_FLAGS, ...server, ...flags }
    return { flags: merged, isEnabled: (key) => merged[key], setServer }
  }, [flags, server])
  return <FeatureFlagsContext.Provider value={value}>{children}</FeatureFlagsContext.Provider>
}

function useFlagsContext(): FeatureFlagsValue {
  const ctx = useContext(FeatureFlagsContext)
  if (!ctx) {
    throw new Error('useFeature must be used within FeatureFlagsProvider')
  }
  return ctx
}

export function useFeature(key: FeatureFlagKey): boolean {
  return useFlagsContext().isEnabled(key)
}

/**
 * Reads the server's switches for the signed in shell (the endpoint needs a session). A failed read keeps the client
 * defaults: a flag endpoint that is down must not take sections away.
 */
export function ServerFeatureFlags() {
  const { setServer } = useFlagsContext()
  const states = useQuery({
    queryKey: ['system', 'feature-flags'],
    queryFn: () => systemApi.featureFlags(),
    staleTime: 60_000,
  })
  useEffect(() => {
    if (states.data) {
      setServer(serverFlags(states.data))
    }
  }, [states.data, setServer])
  return null
}
