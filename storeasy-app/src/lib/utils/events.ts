import { APP_KEY } from '@/config'

// Window CustomEvent names the app owns: `storeasy:auth-lost`.
export function appEvent(name: string): string {
  return `${APP_KEY}:${name}`
}
