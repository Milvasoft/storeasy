import { API, BASE_URL, apiCall, apiRaw } from '../support/api'
import { expect, signIn, test } from '../support/fixtures'

// Tools of the remote MCP (storeasy-api/src/Storeasy.Api/Mcp, README "144 tools").
const MCP_TOOL_COUNT = 144

interface CurrentToken {
  kind?: string | number
  prefix?: string
  organizationId?: number
  readOnly?: boolean
}

/** One JSON-RPC call to the streamable HTTP MCP endpoint; the answer is JSON or one SSE "data:" event. */
async function mcp(token: string, body: object, sessionId?: string) {
  const response = await fetch(`${BASE_URL}/mcp`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
      Accept: 'application/json, text/event-stream',
      ...(sessionId ? { 'Mcp-Session-Id': sessionId } : {}),
    },
    body: JSON.stringify(body),
  })
  const text = await response.text()
  const json = text.trimStart().startsWith('{')
    ? text
    : text
        .split('\n')
        .filter((line) => line.startsWith('data:'))
        .map((line) => line.slice(5).trim())
        .join('')
  return { response, sessionId: response.headers.get('mcp-session-id') ?? sessionId, body: json ? JSON.parse(json) : null }
}

test('creates a personal access token in the UI and uses it for MCP and tokens/current', async ({ page, user, organization }) => {
  await signIn(page, user)
  await page.goto('/developer')
  await page.getByRole('button', { name: 'Token oluştur' }).first().click()
  const form = page.getByRole('dialog', { name: 'Yeni token' })
  await form.getByLabel('Ad', { exact: true }).fill('e2e claude')
  await form.getByRole('button', { name: /token oluştur/i }).click()

  const shown = page.getByRole('dialog', { name: '"e2e claude" hazır' })
  const value = (await shown.locator('.secret span').first().textContent())?.trim() ?? ''
  expect(value.length).toBeGreaterThan(20)
  await shown.getByText("Token'ı güvenli bir yere kopyaladım").click()
  await shown.getByRole('button', { name: 'Bitti' }).click()
  await expect(page.getByRole('cell', { name: 'e2e claude', exact: true })).toBeVisible()

  // The token describes itself and never returns its value.
  const current = await apiCall<CurrentToken>('GET', '/tokens/current', undefined, { token: value })
  expect(JSON.stringify(current)).not.toContain(value)
  expect(current.organizationId).toBe(organization.id)

  // MCP over HTTP through the web origin: initialize, initialized, tools/list.
  const init = await mcp(value, {
    jsonrpc: '2.0',
    id: 1,
    method: 'initialize',
    params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'storeasy-e2e', version: '1.0.0' } },
  })
  expect(init.response.status).toBe(200)
  expect(init.body.result.serverInfo).toBeTruthy()
  await mcp(value, { jsonrpc: '2.0', method: 'notifications/initialized' }, init.sessionId ?? undefined)
  const list = await mcp(value, { jsonrpc: '2.0', id: 2, method: 'tools/list', params: {} }, init.sessionId ?? undefined)
  expect(list.response.status).toBe(200)
  const tools = list.body.result.tools as { name: string }[]
  expect(tools).toHaveLength(MCP_TOOL_COUNT)
  expect(tools.map((t) => t.name)).toContain('apps_summary')

  // A read tool answers through the same token.
  const call = await mcp(
    value,
    { jsonrpc: '2.0', id: 3, method: 'tools/call', params: { name: 'rules_catalog', arguments: {} } },
    init.sessionId ?? undefined,
  )
  expect(call.body.result.isError).toBeFalsy()

  // Without a token the endpoint refuses.
  const anonymous = await fetch(`${BASE_URL}/mcp`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' },
    body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'tools/list' }),
  })
  expect(anonymous.status).toBe(401)
})

test('creates a CI token and reads tokens/current with it', async ({ user, organization }) => {
  const created = await apiCall<{ token?: string; value?: string; id: number }>(
    'POST',
    '/artifacts/ci-token',
    { OrganizationId: organization.id, Name: 'e2e pipeline', AllApps: true, ExpiryDays: 30 },
    { token: user.token },
  )
  const value = created.token ?? created.value ?? ''
  expect(value.length).toBeGreaterThan(20)

  const current = await apiCall<CurrentToken>('GET', '/tokens/current', undefined, { token: value })
  expect(JSON.stringify(current)).not.toContain(value)
  expect(current.organizationId).toBe(organization.id)

  // A CI token is a machine token: endpoints without [AllowMachineToken] refuse it.
  const refused = await apiRaw('GET', '/Organizations/mine', undefined, { token: value })
  expect([401, 403]).toContain(refused.status)
  expect(API).toContain('/api/v1.0')
})
