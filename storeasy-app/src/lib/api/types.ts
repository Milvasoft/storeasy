// Backend contract (Milvasoft, storeasy-api). JSON keys are PascalCase (pascalizeKeys) and enums are numeric.
// Keep these in sync with Storeasy.Application DTOs and Storeasy.Domain enums: a renamed field breaks the build here and
// in the test factories.

/** A calendar day, "yyyy-MM-dd" (backend DateOnly). Never parse with new Date(value). */
export type DateOnly = string
/** A moment, ISO 8601 UTC ("2026-11-18T09:12:00Z"). */
export type IsoDateTime = string

export interface ResponseMessage {
  Key?: string
  Message?: string
  Type?: number
}

export interface ApiResponse<T> {
  IsSuccess: boolean
  StatusCode: number
  Messages?: ResponseMessage[]
  Data: T
}

export interface ApiListResponse<T> {
  IsSuccess: boolean
  StatusCode: number
  Messages?: ResponseMessage[]
  Data: T[]
  CurrentPageNumber?: number | null
  TotalPageCount?: number | null
  TotalDataCount?: number | null
}

// ---- Enums: an `as const` object plus a type of the same name. Values mirror Storeasy.Domain.Enums. ----

export const UserType = { Manager: 1, AppUser: 2 } as const
export type UserType = (typeof UserType)[keyof typeof UserType]

export const OrgRole = { Owner: 1, Admin: 2, Editor: 3, Viewer: 4 } as const
export type OrgRole = (typeof OrgRole)[keyof typeof OrgRole]

export const OrgPermission = {
  View: 1,
  EditContent: 2,
  ReleaseTest: 3,
  ReleaseProduction: 4,
  SubmitForReview: 5,
  ReplyReviews: 6,
  ManageConnections: 7,
  ManageWebhooks: 8,
  ManageMembers: 9,
  ManageOrganization: 10,
} as const
export type OrgPermission = (typeof OrgPermission)[keyof typeof OrgPermission]

// Microsoft is planned (design 14.7, backend gap); the client already knows the value the API will use.
export const StoreKind = { Apple: 1, Google: 2, Microsoft: 3 } as const
export type StoreKind = (typeof StoreKind)[keyof typeof StoreKind]

export const ConnectionStatus = { NotVerified: 1, Verified: 2, Rejected: 3, Unreachable: 4 } as const
export type ConnectionStatus = (typeof ConnectionStatus)[keyof typeof ConnectionStatus]

export const ActionChannel = { Web: 1, Mcp: 2, Api: 3, System: 4 } as const
export type ActionChannel = (typeof ActionChannel)[keyof typeof ActionChannel]

export const AuditAction = {
  OrganizationCreated: 1,
  OrganizationUpdated: 2,
  MemberInvited: 10,
  InvitationRevoked: 11,
  MemberJoined: 12,
  MemberRoleChanged: 13,
  MemberRemoved: 14,
  ConnectionAdded: 20,
  ConnectionUpdated: 21,
  ConnectionRemoved: 22,
  ConnectionVerified: 23,
} as const
export type AuditAction = (typeof AuditAction)[keyof typeof AuditAction]

// ---- Account ----

export interface MilvaToken {
  AccessToken: string
  RefreshToken: string
  ExpiresIn: number
  RefreshTokenExpiresIn: number
}

export interface LoginResponseDto {
  Id: number
  UserType: UserType
  UserTypeDescription?: string | null
  Token: MilvaToken
}

export interface NameIntNavigationDto {
  Id: number
  Name?: string | null
}

export interface AccountDetailDto {
  Id: number
  UserName: string
  Email?: string | null
  Name?: string | null
  Surname?: string | null
  /** Platform roles (Manager users), not organization roles. */
  Roles?: NameIntNavigationDto[] | null
}

// ---- Organizations ----

export interface MyOrganizationDto {
  Id: number
  Name: string
  Slug: string
  Role: OrgRole
  Permissions: OrgPermission[]
}

export interface OrganizationMemberDto {
  Id: number
  UserId: number
  Email?: string | null
  Name?: string | null
  Surname?: string | null
  Role: OrgRole
  JoinDate?: IsoDateTime | null
}

export interface OrganizationInvitationDto {
  Id: number
  Email: string
  Role: OrgRole
  ExpiryDate: IsoDateTime
  CreationDate?: IsoDateTime | null
}

export interface MemberListDto {
  Members: OrganizationMemberDto[]
  /** Filled only for callers with ManageMembers. */
  Invitations?: OrganizationInvitationDto[] | null
}

export interface InvitationCreatedDto {
  Id: number
  /** The one time token for the accept link; returned only in this response. */
  Token: string
  ExpiryDate: IsoDateTime
}

export interface StoreAuditLogDto {
  Id: number
  Date: IsoDateTime
  UserName?: string | null
  Channel: ActionChannel
  Action: AuditAction
  Store?: StoreKind | null
  TargetId?: string | null
  Summary?: string | null
  /** Free text today (often a JSON string); structured before/after is a backend gap (design 15.16). */
  Details?: string | null
}

// ---- Store connections ----

export interface ConnectionFingerprint {
  KeyId?: string | null
  IssuerIdSuffix?: string | null
  ServiceAccountEmail?: string | null
  ProjectId?: string | null
}

export interface StoreConnectionDto {
  Id: number
  Store: StoreKind
  Name: string
  Fingerprint?: ConnectionFingerprint | null
  Status: ConnectionStatus
  LastVerifiedDate?: IsoDateTime | null
  LastVerifyError?: string | null
  CreationDate?: IsoDateTime | null
}
