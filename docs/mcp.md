# MCP

storeasy exposes its features to MCP clients (Claude Code, Claude Desktop and others). Every tool calls the same
application command as the web app, so roles, diffs, dry runs, confirmations and the audit log are identical; audit
entries record the channel as MCP.

> Status: the remote endpoint and personal access tokens are being built (plan phase 3). The local bridge
> (`storeasy-mcp`) is ready in [`tools/storeasy-cli`](../tools/storeasy-cli/README.md) against the contract in
> [`tools/CONTRACT.md`](../tools/CONTRACT.md).

## Two ways to connect

| | Remote MCP | Local bridge `storeasy-mcp` |
|---|---|---|
| Transport | Streamable HTTP at `https://<your storeasy>/mcp` | stdio, runs on your machine, forwards to the remote `/mcp` |
| Local files | No: file arguments take asset ids | Yes: file and folder arguments take local paths; the bridge uploads them first |
| Extra tools | | `artifact_upload` (send an IPA, AAB or MSIX from disk) |
| Needs | A personal access token | Node 22+ and a personal access token (or `storeasy login`) |

Use the bridge when you work with screenshot folders or build files on your machine; otherwise remote is enough.

## Personal access token

Create one in storeasy under **Developer > MCP and tokens**. A token:

- starts with `sea_` and is shown once; storeasy keeps only its SHA-256 hash and a short prefix;
- is bound to you and one organization; on each request its permission is the lower of the token's and your current
  role in that organization, so a role change or removal applies at once;
- can be read only (Viewers can only create read only tokens) and has an expiry date;
- can be revoked at any time; a revoked token answers 401.

Keep the token in your environment or a password manager, never in a committed file.

## Claude Code

Remote:

```bash
claude mcp add --transport http storeasy https://storeasy.example.com/mcp \
  --header "Authorization: Bearer $STOREASY_TOKEN"
```

Local bridge:

```bash
claude mcp add storeasy-local -e STOREASY_URL=https://storeasy.example.com -e STOREASY_TOKEN="$STOREASY_TOKEN" \
  -- npx -y -p storeasy storeasy-mcp
```

Or in a project `.mcp.json` (Claude Code expands `${STOREASY_TOKEN}` from your environment, so the file holds no
secret):

```json
{
  "mcpServers": {
    "storeasy": {
      "type": "http",
      "url": "https://storeasy.example.com/mcp",
      "headers": { "Authorization": "Bearer ${STOREASY_TOKEN}" }
    },
    "storeasy-local": {
      "command": "npx",
      "args": ["-y", "-p", "storeasy", "storeasy-mcp"],
      "env": { "STOREASY_URL": "https://storeasy.example.com", "STOREASY_TOKEN": "${STOREASY_TOKEN}" }
    }
  }
}
```

Use one of the two entries, not both.

## Claude Desktop

Claude Desktop starts local servers from `claude_desktop_config.json` (macOS:
`~/Library/Application Support/Claude/claude_desktop_config.json`, Windows: `%APPDATA%\Claude\claude_desktop_config.json`).
Use the bridge, and sign in once with the CLI so the file holds no token:

```bash
npx -y storeasy login --url https://storeasy.example.com
```

```json
{
  "mcpServers": {
    "storeasy": {
      "command": "npx",
      "args": ["-y", "-p", "storeasy", "storeasy-mcp"],
      "env": { "STOREASY_URL": "https://storeasy.example.com" }
    }
  }
}
```

Without `STOREASY_TOKEN` the bridge uses the token saved by `storeasy login` (owner-only file, see the
[CLI README](../tools/storeasy-cli/README.md#sign-in)). Restart Claude Desktop after editing the file.

## How file arguments work

A remote tool that takes files marks those arguments with `x-storeasy-upload` (kind `file`, `files` or `folder`). The
bridge shows them to your client as paths, uploads the files to storeasy's storage with presigned URLs, and calls the
remote tool with the asset ids. Folders are read in natural name order (`2.png` before `10.png`), not recursively.
Details: [CONTRACT section 6](../tools/CONTRACT.md#6-remote-mcp-mcp-plan-26-and-117-design-1615).

## Confirmation rules

The same rules as the web app, enforced on the server:

- **Reads** (apps, readiness, plans, jobs, rules, compliance) need any role.
- **Writes are plans first.** A write tool reads the store live and returns a change plan (the diff); nothing is
  written. Applying the plan is a separate call, checked against the plan's permission, and storeasy reads the store
  again: if the touched values changed meanwhile, the apply is refused (409) and a new plan is needed. Plans expire
  after 48 hours.
- **Review submission and production releases** need the matching permission (`SubmitForReview`, `ReleaseProduction`;
  Admin and Owner by default), `confirm: true` and the typed confirmation (app name and version), exactly like the
  web app's confirmation step.
- **Read only tokens** can call read tools only (403 otherwise).
- **Assisted steps** (work the stores offer no API for) return a checklist and links. storeasy and its tools never
  store or type a store password; the store session stays yours.

Keep your MCP client's permission prompts on for storeasy's write tools: approving a plan is an action on your store.

## Troubleshooting

| Symptom | Check |
|---|---|
| 401 | Token expired or revoked, or the `Authorization` header is missing. |
| 403 `OrganizationForbidden` | The token is read only or your role lacks the permission. |
| `AssetNotFound` from a tool | You passed a path to the remote server: use the bridge, or pass asset ids. |
| Bridge does not start | Node 22+ on `PATH`; run `npx -y -p storeasy storeasy-mcp` in a terminal to see the error. |
