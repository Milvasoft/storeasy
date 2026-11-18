// SPDX-License-Identifier: AGPL-3.0-only
import { redact } from './redact.js'

export interface Writer {
  write(chunk: string): unknown
  isTTY?: boolean
}

export interface Io {
  stdout: Writer
  stderr: Writer
}

export const processIo = (): Io => ({ stdout: process.stdout, stderr: process.stderr })

/**
 * Every line the tools print goes through here and is redacted. Results go to stdout, progress and messages to
 * stderr, so `--json` output stays parseable.
 */
export class Logger {
  private progressOpen = false

  constructor(
    readonly io: Io,
    private readonly opts: { quiet?: boolean } = {},
  ) {}

  /** A result line (stdout). */
  out(text: string): void {
    this.endProgress()
    this.io.stdout.write(`${redact(text)}\n`)
  }

  /** A message line (stderr). */
  info(text: string): void {
    if (this.opts.quiet) {
      return
    }
    this.endProgress()
    this.io.stderr.write(`${redact(text)}\n`)
  }

  warn(text: string): void {
    this.endProgress()
    this.io.stderr.write(`warning: ${redact(text)}\n`)
  }

  error(text: string): void {
    this.endProgress()
    this.io.stderr.write(`error: ${redact(text)}\n`)
  }

  /** A CI runner command (`::add-mask::`, `##vso[...]`). Written as is: redacting it would defeat the mask. */
  command(line: string): void {
    this.endProgress()
    this.io.stdout.write(`${line}\n`)
  }

  /** One progress line; on a terminal it is rewritten in place. */
  progress(text: string): void {
    if (this.opts.quiet) {
      return
    }
    if (this.io.stderr.isTTY) {
      this.io.stderr.write(`\r\x1b[2K${redact(text)}`)
      this.progressOpen = true
    } else {
      this.io.stderr.write(`${redact(text)}\n`)
    }
  }

  endProgress(): void {
    if (this.progressOpen) {
      this.io.stderr.write('\n')
      this.progressOpen = false
    }
  }
}

export function formatBytes(n: number): string {
  if (n < 1024) {
    return `${n} B`
  }
  const units = ['KB', 'MB', 'GB', 'TB']
  let v = n / 1024
  let i = 0
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024
    i++
  }
  return `${v.toFixed(1)} ${units[i]}`
}
