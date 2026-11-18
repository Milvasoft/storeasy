# storeasy client tools

Everything that runs outside the storeasy server: on a developer machine, in CI, or next to a local MCP client. Phase 3
of the plan (`docs/urun-plani.md` sections 2.6, 11.7, 11.8; design files 13 and 16).

| Folder | What | Ships as |
|---|---|---|
| `storeasy-cli/` | `storeasy` CLI (login, whoami, apps list, jobs get, upload) and the `storeasy-mcp` stdio bridge | npm package `storeasy` (bins `storeasy`, `storeasy-mcp`) |
| `upload-action/` | GitHub Action, OIDC or CI token | `storeasy/upload-action@v1` (node24, `dist/index.cjs`) |
| `azure-devops-task/` | Azure DevOps task `StoreasyUpload@1`, workload identity or CI token | Marketplace extension (`vss-extension.json`, Node20_1 handler) |
| `CONTRACT.md` | The HTTP contract these tools need from `storeasy-api` | |
| `scripts/` | Shared bundling and repository checks | |

User guides: [docs/mcp.md](../docs/mcp.md) and [docs/ci-intake.md](../docs/ci-intake.md).

All of it is AGPL-3.0-only, Node 22+ for development. Runtime dependencies: `@modelcontextprotocol/sdk` (MIT) in the
npm package only; the action and the task bundle nothing but storeasy's own code.

## How a build reaches the store

```text
CI job --OIDC token (aud storeasy)--> POST /api/v1.0/oidc/exchange --> 15 minute upload token (one app)
       --POST /api/v1.0/apps/{appId}/artifacts {fileName, size, sha256}--> session, part size, presigned part URLs
       --PUT parts to S3 (parallel, retried, resumable)--> ETags
       --PUT /api/v1.0/artifacts/{sessionId}/complete--> storeasy parses, checks, applies the delivery rule
       --GET /api/v1.0/artifacts/session?SessionId= (with --wait)--> Delivered / Rejected / Held / Failed -> exit code
```

Same SHA-256 again: no upload, no second delivery, exit 0 (design 13.17). Identity without OIDC: a CI token
(`sci_...`) in `STOREASY_TOKEN` or the action/task `token` input. On a laptop: `storeasy login` with a personal access
token (`sea_...`).

## MCP

- Remote: `claude mcp add --transport http storeasy https://storeasy.milvasoft.com/mcp --header "Authorization: Bearer $STOREASY_TOKEN"`.
- Local bridge, needed when tools take local files or folders (screenshot folders, AAB, IPA):
  `claude mcp add storeasy-local -e STOREASY_URL=... -e STOREASY_TOKEN=sea_... -- npx -y -p storeasy storeasy-mcp`.
  Remote tools mark asset id arguments with `x-storeasy-upload` (CONTRACT 6.1); the bridge shows them as path
  arguments, uploads the files and calls the tool with the asset ids. It adds `artifact_upload`.

## Secrets

Tokens are never printed or written anywhere except the 0600 config file of `storeasy login`. Every line of output
passes through `redact()` (storeasy token prefixes, bearer headers, JWTs, PEM blocks, signed URL queries, and the
exact values seen at run time). In GitHub Actions and Azure Pipelines the OIDC and upload tokens are also registered
with the runner's masking. Tests check this.

## Development

```bash
cd tools
npm install
npm run build    # tsc for storeasy-cli, esbuild bundles for the action and the task
npm test         # vitest in every package, against a local fake API, S3 and remote MCP (no network)
npm run lint     # eslint, text rules (SPDX headers, no em or en dashes), bundle freshness, licenses, tsc
```

After changing `storeasy-cli/src` or an action or task source, run `npm run build` and commit the bundles; `npm run
lint` fails on a stale bundle.

Nothing here is published: npm, the GitHub Marketplace and the Visual Studio Marketplace need their own release steps
(package names, publisher ids, a dedicated `storeasy/upload-action` repository with a `v1` tag).
