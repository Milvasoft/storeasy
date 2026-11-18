// SPDX-License-Identifier: AGPL-3.0-only
import { mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import type { Io } from '../src/log.js'

export function tempDir(): string {
  return mkdtempSync(join(tmpdir(), 'storeasy-test-'))
}

/** A file of `size` deterministic bytes. */
export function makeFile(dir: string, name: string, size: number): string {
  const buf = Buffer.alloc(size)
  for (let i = 0; i < size; i++) {
    buf[i] = (i * 31 + (i >> 8)) & 0xff
  }
  const p = join(dir, name)
  writeFileSync(p, buf)
  return p
}

export interface Captured extends Io {
  out: () => string
  err: () => string
  all: () => string
}

export function captureIo(tty = false): Captured {
  let out = ''
  let err = ''
  return {
    stdout: { write: (s: string) => (out += s), isTTY: tty },
    stderr: { write: (s: string) => (err += s), isTTY: tty },
    out: () => out,
    err: () => err,
    all: () => out + err,
  }
}

export const noSleep = async () => undefined

/** Env for a test: isolated config dir, the fake server URL, nothing from the real environment. */
export function testEnv(baseUrl: string, extra: Record<string, string | undefined> = {}): Record<string, string | undefined> {
  return { STOREASY_CONFIG_DIR: tempDir(), STOREASY_URL: baseUrl, ...extra }
}
