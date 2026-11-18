import { apiDelete, apiGet, apiPost } from '@/lib/api/http'
import type { CreatePersonalAccessTokenInput, PersonalAccessTokenCreatedDto, PersonalAccessTokenDto } from './types'

// storeasy-api TokensController. The caller's own tokens of every organization; created and revoked from a web session.
export const tokensApi = {
  list: () => apiGet<PersonalAccessTokenDto[]>('/Tokens'),

  create: (input: CreatePersonalAccessTokenInput) => apiPost<PersonalAccessTokenCreatedDto>('/Tokens/token', input, { idempotent: true }),

  revoke: (tokenId: number) => apiDelete<number>('/Tokens/token', undefined, { TokenId: tokenId }),
}

export const tokenKeys = {
  all: ['tokens'] as const,
  list: () => ['tokens', 'list'] as const,
}
