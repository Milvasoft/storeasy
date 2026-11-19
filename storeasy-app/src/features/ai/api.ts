import { useQuery } from '@tanstack/react-query'
import { aiApi } from '@/lib/api/endpoints'

// The calls live in lib/api/endpoints.ts (aiApi) with the other organization endpoints; this file holds the query keys,
// the settings hook and the key shape check of the AI screens.
export { aiApi }
export type { AiSettingsDto, GeneratedReplyDraftDto } from '@/lib/api/types'

export const aiKeys = {
  all: ['ai'] as const,
  settings: (organizationId: number) => ['ai', 'settings', organizationId] as const,
}

/** The organization's AI settings (every member may read them). */
export function useAiSettings(organizationId: number) {
  return useQuery({
    queryKey: aiKeys.settings(organizationId),
    queryFn: () => aiApi.settings(organizationId),
    staleTime: 60_000,
  })
}

/** Anthropic API keys start with this; the API checks the same shape. */
export const ANTHROPIC_KEY_PREFIX = 'sk-ant-'
const KEY_PATTERN = /^sk-ant-[A-Za-z0-9_-]{5,}$/

export function looksLikeAnthropicKey(value: string): boolean {
  const key = value.trim()
  return key.length <= 300 && KEY_PATTERN.test(key)
}
