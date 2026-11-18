#!/usr/bin/env node
// SPDX-License-Identifier: AGPL-3.0-only
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import { resolveAuth } from './auth.js'
import { connectRemote, createBridge } from './bridge.js'
import { resolveBaseUrl } from './config.js'
import { errorMessage } from './errors.js'

// storeasy-mcp: stdio MCP server for local clients (Claude Code and others). Proxies the remote storeasy MCP at
// <STOREASY_URL>/mcp and uploads local files for tools that take them. stdout carries the MCP protocol only;
// diagnostics go to stderr, redacted.
try {
  const env = process.env
  const auth = resolveAuth({ env, baseUrl: resolveBaseUrl(env), client: 'storeasy-mcp', oidc: false })
  if (auth.kind === 'none') {
    process.stderr.write('storeasy-mcp: no token. Set STOREASY_TOKEN to a personal access token (sea_...) or run "storeasy login".\n')
  }
  const server = createBridge({ api: auth.api, connect: () => connectRemote(auth.api), env })
  await server.connect(new StdioServerTransport())
  process.stderr.write(`storeasy-mcp: serving ${auth.api.baseUrl}/mcp (${auth.describe})\n`)
} catch (err) {
  process.stderr.write(`storeasy-mcp: ${errorMessage(err)}\n`)
  process.exitCode = 1
}
