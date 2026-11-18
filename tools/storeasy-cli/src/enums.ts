// SPDX-License-Identifier: AGPL-3.0-only

// Numeric enums of the API (tools/CONTRACT.md section 7). The API sends numbers; names are accepted too.

export const StoreKind = { Apple: 1, Google: 2, Microsoft: 3 } as const
export const ArtifactSource = {
  GitHubActions: 1,
  AzureDevOps: 2,
  GitLab: 3,
  Cli: 4,
  Rest: 5,
  Eas: 6,
  Webhook: 7,
  Manual: 8,
  Sync: 9,
} as const
export type ArtifactSource = (typeof ArtifactSource)[keyof typeof ArtifactSource]
export const OidcProvider = { GitHub: 1, AzureDevOps: 2, GitLab: 3 } as const
export type OidcProvider = (typeof OidcProvider)[keyof typeof OidcProvider]
export const SessionStatus = {
  Uploading: 1,
  Parsing: 2,
  Checking: 3,
  Delivering: 4,
  Delivered: 5,
  Rejected: 6,
  Duplicate: 7,
  Held: 8,
  Failed: 9,
} as const
export const StepKind = { Upload: 1, Parse: 2, Check: 3, Rule: 4, Deliver: 5, Report: 6 } as const
export const StepStatus = { Pending: 1, Running: 2, Done: 3, Failed: 4, Skipped: 5 } as const
export const TokenKind = { PersonalAccessToken: 1, CiToken: 2, UploadToken: 3 } as const
export const StoreJobStatus = { Queued: 1, Running: 2, Succeeded: 3, Failed: 4, Cancelled: 5 } as const
export const AssetPurpose = { Screenshot: 1, Graphic: 2, MockupOutput: 3, BuildArtifact: 4 } as const

/** Name of a numeric enum value; accepts a name as is. */
export function enumName(map: Record<string, number>, value: unknown): string {
  if (typeof value === 'string') {
    return value
  }
  const hit = Object.entries(map).find(([, v]) => v === value)
  return hit ? hit[0] : String(value ?? '')
}

/** Numeric value of an enum given as a number or a (case insensitive) name. */
export function enumValue(map: Record<string, number>, value: unknown): number | undefined {
  if (typeof value === 'number') {
    return value
  }
  if (typeof value === 'string') {
    const hit = Object.entries(map).find(([k]) => k.toLowerCase() === value.toLowerCase())
    if (hit) {
      return hit[1]
    }
    const n = Number(value)
    return Number.isInteger(n) ? n : undefined
  }
  return undefined
}

/** `--store` values. */
export function parseStore(value: string | undefined): number | undefined {
  if (value === undefined) {
    return undefined
  }
  const aliases: Record<string, number> = {
    apple: StoreKind.Apple,
    ios: StoreKind.Apple,
    appstore: StoreKind.Apple,
    testflight: StoreKind.Apple,
    google: StoreKind.Google,
    play: StoreKind.Google,
    android: StoreKind.Google,
    microsoft: StoreKind.Microsoft,
    windows: StoreKind.Microsoft,
    msstore: StoreKind.Microsoft,
  }
  return aliases[value.toLowerCase()]
}
export const StoreJobStepStatus = { Running: 1, Succeeded: 2, Failed: 3, Skipped: 4 } as const
