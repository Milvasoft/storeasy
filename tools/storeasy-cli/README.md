# storeasy (CLI) and storeasy-mcp

One npm package, two commands:

- `storeasy`: upload an IPA, AAB, APK or MSIX from a developer machine or any CI, and read apps, jobs and the token
  in use.
- `storeasy-mcp`: a stdio MCP server for local MCP clients (Claude Code and others). It proxies the remote storeasy
  MCP at `<url>/mcp` and lets its tools take local file and folder paths: the files are uploaded first (presigned
  URLs) and the remote tool receives asset ids.

Node 22 or later. License AGPL-3.0-only. The server side it talks to is specified in `../CONTRACT.md`.

## Install

```bash
npm install -g storeasy        # or run it without installing: npx storeasy ...
```

## Sign in

Create a personal access token (`sea_...`) in storeasy under Developer > MCP and tokens, then:

```bash
storeasy login                                   # prompts without echo
storeasy login --url https://storeasy.example.com   # self-hosted
echo "$TOKEN" | storeasy login --with-token      # from a password manager
storeasy whoami
storeasy logout
```

The token is saved with owner-only permissions (0600) in:

| OS | Path |
|---|---|
| Windows | `%APPDATA%\storeasy\config.json` |
| macOS | `~/Library/Application Support/storeasy/config.json` |
| Linux and others | `$XDG_CONFIG_HOME/storeasy/config.json` or `~/.config/storeasy/config.json` |

`STOREASY_CONFIG_DIR` overrides the directory. The token is never printed; `whoami` shows its prefix only.

## Auth precedence

1. `STOREASY_TOKEN` (a CI token `sci_...` from a secret variable, or a PAT).
2. OIDC in CI, for `upload` only (the exchange is per app):
   - GitHub Actions with `permissions: id-token: write`: the CLI asks for a token with audience `storeasy`;
   - Azure Pipelines: `SYSTEM_OIDCREQUESTURI` plus a service connection id (`--service-connection` or
     `STOREASY_SERVICE_CONNECTION_ID`) and `SYSTEM_ACCESSTOKEN` mapped into the step's env;
   - any CI that already holds an OIDC token (GitLab `id_tokens`): put it in `STOREASY_OIDC_TOKEN`.

   The OIDC token is exchanged for a 15 minute upload token for that app, renewed automatically for long waits.
3. The PAT saved by `storeasy login`.

Base URL: `--url`, then `STOREASY_URL`, then the saved config, then `https://storeasy.milvasoft.com`. Plain `http` is
accepted only for localhost.

## Commands

```text
storeasy upload <file> --app <id> [--store apple|google|microsoft] [--track <name>] [--group <name>]
                [--notes <text> | --notes-file <path>] [--wait] [--timeout <minutes>] [--json]
storeasy whoami [--json]
storeasy apps list [--json]
storeasy jobs get <jobId> [--json]
```

`upload` hashes the file (SHA-256), opens an upload session, sends the parts in parallel to their presigned URLs and
completes the session. A failed part is retried (4 attempts with backoff); an expired URL is refreshed; if the run
still fails, the same command later resumes with the missing parts only. The same file twice is recognized by its
SHA-256 and not delivered again. `--track` and `--group` override the app's delivery rule for this upload; the server
only accepts test channels.

`--wait` follows the session (and, for a duplicate, the original session) and prints each step.

| Exit code | Meaning |
|---|---|
| 0 | Delivered, duplicate, held for approval, or uploaded without `--wait` |
| 1 | Usage, file or network error |
| 2 | Package rejected (package name, version code, checks) |
| 3 | Identity: no or bad token, OIDC claim mismatch, permission |
| 4 | The store refused or failed the delivery |
| 5 | `--wait` timed out (the delivery continues on the server) |

## storeasy-mcp

```bash
claude mcp add storeasy-local -e STOREASY_URL=https://storeasy.milvasoft.com -e STOREASY_TOKEN=sea_... -- npx -y -p storeasy storeasy-mcp
```

or in `.mcp.json`, reading the token from your environment:

```json
{
  "mcpServers": {
    "storeasy-local": {
      "command": "npx",
      "args": ["-y", "-p", "storeasy", "storeasy-mcp"],
      "env": { "STOREASY_URL": "https://storeasy.milvasoft.com", "STOREASY_TOKEN": "${STOREASY_TOKEN}" }
    }
  }
}
```

Without `STOREASY_TOKEN` the bridge uses the saved `storeasy login`. Every remote tool is listed as is, except that
arguments the server marks with `x-storeasy-upload` (CONTRACT 6.1) take local paths:

- `file`: one path; `files`: a list of paths; `folder`: a folder whose matching files are uploaded in natural name
  order (`2.png` before `10.png`, hidden files skipped, not recursive).
- Relative paths resolve against the directory the bridge was started in; `~` is the home directory.
- Numbers are passed through as asset ids.

The bridge adds one local tool, `artifact_upload` (`path`, `appId`, optional `store`, `track`, `group`, `notes`,
`wait`), which runs the same upload as the CLI.

The remote MCP needs no bridge when no local files are involved:
`claude mcp add --transport http storeasy https://storeasy.milvasoft.com/mcp --header "Authorization: Bearer $STOREASY_TOKEN"`.

## Library

The package also exports its building blocks (`runUpload`, `uploadArtifact`, `waitForSession`, `resolveAuth`,
`redact`, ...); the GitHub Action and the Azure DevOps task in this repository bundle them.

## Development

```bash
cd tools
npm install
npm run build
npm test
npm run lint
```

Tests run against a local fake of the API and of S3 (`test/fakeServer.ts`), never the network.
