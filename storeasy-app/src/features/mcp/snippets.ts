import { API_BASE } from '@/config'

// The MCP setup snippets (docs/mcp.md, tools/README.md), built in the browser from the installation's own address. The
// local bridge is the `storeasy-mcp` bin of the `storeasy` npm package, so npx needs `-p storeasy`.

export const BRIDGE_COMMAND = 'npx -y -p storeasy storeasy-mcp'
export const TOKEN_ENV = 'STOREASY_TOKEN'

/** The public origin of this installation: the API's host, or the page's own when the API is same-origin. */
export function publicOrigin(): string {
  const fromApi = API_BASE.replace(/\/api\/v[\d.]+$/, '')
  return fromApi || window.location.origin
}

/** An absolute address for a path the API answers relative to the origin (`/api/webhooks/...`). */
export function absoluteUrl(path: string): string {
  return /^https?:\/\//.test(path) ? path : `${publicOrigin()}${path.startsWith('/') ? '' : '/'}${path}`
}

export function mcpUrl(): string {
  return `${publicOrigin()}/mcp`
}

/** `claude mcp add` for the remote server; `token` is the value, a prefix placeholder or `$STOREASY_TOKEN`. */
export function claudeCodeRemote(token: string): string {
  return `claude mcp add --transport http storeasy ${mcpUrl()} \\\n  --header "Authorization: Bearer ${token}"`
}

export function claudeCodeBridge(token: string): string {
  return `claude mcp add storeasy-local -e STOREASY_URL=${publicOrigin()} -e STOREASY_TOKEN=${token} \\\n  -- ${BRIDGE_COMMAND}`
}

/** A project `.mcp.json`: Claude Code expands ${STOREASY_TOKEN} from the environment, so the file holds no secret. */
export function mcpJson(): string {
  return JSON.stringify(
    {
      mcpServers: {
        storeasy: {
          type: 'http',
          url: mcpUrl(),
          headers: { Authorization: `Bearer \${${TOKEN_ENV}}` },
        },
      },
    },
    null,
    2,
  )
}

export function loginCommand(): string {
  return `npx -y storeasy login --url ${publicOrigin()}`
}

/** claude_desktop_config.json: the bridge, signed in once with `storeasy login` so the file holds no token. */
export function claudeDesktopJson(): string {
  return JSON.stringify(
    {
      mcpServers: {
        storeasy: {
          command: 'npx',
          args: ['-y', '-p', 'storeasy', 'storeasy-mcp'],
          env: { STOREASY_URL: publicOrigin() },
        },
      },
    },
    null,
    2,
  )
}
