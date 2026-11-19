import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const STACK = fileURLToPath(new URL('../../../deploy/e2e/stack.mjs', import.meta.url))

/**
 * The one time setup code of a single team stack, read where an operator reads it: the API's log
 * (`docker compose logs storeasy-api`, through deploy/e2e/stack.mjs setup-code). It is never written anywhere.
 */
export function setupCodeFromLog(): string {
  return execFileSync(process.execPath, [STACK, 'setup-code'], { encoding: 'utf8' }).trim()
}
