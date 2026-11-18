// SPDX-License-Identifier: AGPL-3.0-only
import { chmodSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs'
import { homedir } from 'node:os'
import { dirname, join } from 'node:path'
import { StoreasyError } from './errors.js'

export type Env = Record<string, string | undefined>

export const DEFAULT_URL = 'https://storeasy.milvasoft.com'

/** The saved config. Holds the base URL and the personal access token (PAT) of `storeasy login`. */
export interface FileConfig {
  url?: string
  token?: string
}

/**
 * Config directory, outside any repo:
 *   Windows  %APPDATA%\storeasy
 *   macOS    ~/Library/Application Support/storeasy
 *   others   $XDG_CONFIG_HOME/storeasy or ~/.config/storeasy
 * STOREASY_CONFIG_DIR overrides it (tests, portable setups).
 */
export function configDir(env: Env = process.env, platform: NodeJS.Platform = process.platform): string {
  if (env.STOREASY_CONFIG_DIR) {
    return env.STOREASY_CONFIG_DIR
  }
  if (platform === 'win32' && env.APPDATA) {
    return join(env.APPDATA, 'storeasy')
  }
  if (platform === 'darwin') {
    return join(env.HOME ?? homedir(), 'Library', 'Application Support', 'storeasy')
  }
  return join(env.XDG_CONFIG_HOME ?? join(env.HOME ?? homedir(), '.config'), 'storeasy')
}

export function configPath(env: Env = process.env): string {
  return join(configDir(env), 'config.json')
}

export function readConfig(env: Env = process.env): FileConfig {
  const p = configPath(env)
  if (!existsSync(p)) {
    return {}
  }
  try {
    const parsed = JSON.parse(readFileSync(p, 'utf8')) as unknown
    return parsed && typeof parsed === 'object' ? (parsed as FileConfig) : {}
  } catch {
    throw new StoreasyError(`Config file is not valid JSON: ${p}`)
  }
}

/** Writes a file readable only by the owner (0600, directory 0700), atomically. */
export function writePrivateFile(path: string, content: string): void {
  mkdirSync(dirname(path), { recursive: true, mode: 0o700 })
  const tmp = `${path}.${process.pid}.tmp`
  writeFileSync(tmp, content, { mode: 0o600 })
  // mode on create is masked by umask; chmod makes it exact. On Windows this only toggles read only, and the file
  // stays private through the per-user profile folder ACL.
  chmodSync(tmp, 0o600)
  renameSync(tmp, path)
  chmodSync(path, 0o600)
}

export function writeConfig(cfg: FileConfig, env: Env = process.env): string {
  const p = configPath(env)
  const clean: FileConfig = {}
  if (cfg.url) {
    clean.url = cfg.url
  }
  if (cfg.token) {
    clean.token = cfg.token
  }
  writePrivateFile(p, `${JSON.stringify(clean, null, 2)}\n`)
  return p
}

export function removeToken(env: Env = process.env): boolean {
  const cfg = readConfig(env)
  if (!cfg.token) {
    return false
  }
  delete cfg.token
  if (cfg.url) {
    writeConfig(cfg, env)
  } else {
    rmSync(configPath(env), { force: true })
  }
  return true
}

/** Base URL: --url flag, STOREASY_URL, config, default. No trailing slash. */
export function resolveBaseUrl(env: Env = process.env, flag?: string): string {
  const raw = flag ?? env.STOREASY_URL ?? readConfig(env).url ?? DEFAULT_URL
  let u: URL
  try {
    u = new URL(raw)
  } catch {
    throw new StoreasyError(`Not a valid URL: ${raw}`)
  }
  if (u.protocol !== 'https:' && !isLocalHost(u.hostname)) {
    throw new StoreasyError(`storeasy URL must use https (http is allowed only for localhost): ${raw}`)
  }
  return `${u.origin}${u.pathname.replace(/\/+$/, '')}`
}

function isLocalHost(host: string): boolean {
  return host === 'localhost' || host === '127.0.0.1' || host === '[::1]' || host.endsWith('.localhost')
}
