// SPDX-License-Identifier: AGPL-3.0-only
import { redact } from './redact.js'

/** Process exit codes (design 13.21). */
export const ExitCode = {
  Ok: 0,
  /** Usage error, network error, unexpected server answer. */
  Error: 1,
  /** The package was rejected (wrong package name, failed check). */
  Rejected: 2,
  /** Identity: no token, invalid token, OIDC claim mismatch, missing permission. */
  Auth: 3,
  /** The store refused or failed the delivery. */
  Store: 4,
  /** --wait ran out of time. */
  Timeout: 5,
} as const
export type ExitCode = (typeof ExitCode)[keyof typeof ExitCode]

export interface ErrorDetails {
  status?: number
  keys?: string[]
  exitCode?: ExitCode
}

export class StoreasyError extends Error {
  readonly status?: number
  readonly keys: string[]
  readonly exitCode: ExitCode

  constructor(message: string, details: ErrorDetails = {}) {
    super(redact(message))
    this.name = 'StoreasyError'
    this.status = details.status
    this.keys = details.keys ?? []
    this.exitCode = details.exitCode ?? (details.status === 401 || details.status === 403 ? ExitCode.Auth : ExitCode.Error)
  }

  hasKey(key: string): boolean {
    return this.keys.includes(key)
  }
}

export function errorMessage(err: unknown): string {
  if (err instanceof Error) {
    return redact(err.message)
  }
  return redact(String(err))
}

export function exitCodeOf(err: unknown): ExitCode {
  return err instanceof StoreasyError ? err.exitCode : ExitCode.Error
}
