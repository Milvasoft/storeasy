// Personal access token contract (storeasy-api Storeasy.Application TokenDtos PersonalAccessTokenDto,
// PersonalAccessTokenCreatedDto and the CreatePersonalAccessToken command).
import type { IsoDateTime } from '@/lib/api/types'

/** The expiries the create form offers (design 16.12); the API takes any day count up to a year. */
export const PAT_EXPIRY_DAYS = [30, 90, 365] as const
export type PatExpiryDays = (typeof PAT_EXPIRY_DAYS)[number]

export const PAT_NAME_MAX = 60

export interface PersonalAccessTokenDto {
  Id: number
  Name?: string
  OrganizationId: number
  OrganizationName?: string
  ReadOnly: boolean
  Prefix?: string
  ExpiryDate: IsoDateTime
  LastUseDate?: IsoDateTime | null
  LastClientName?: string | null
  CreationDate?: IsoDateTime | null
  IsExpired: boolean
}

// The value appears in this answer only.
export interface PersonalAccessTokenCreatedDto {
  Id: number
  Token?: string
  Prefix?: string
  ExpiryDate: IsoDateTime
}

export interface CreatePersonalAccessTokenInput {
  OrganizationId: number
  Name: string
  ReadOnly: boolean
  ExpiryDays: number
}
