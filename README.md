# storeasy

Manage App Store Connect, Google Play and Microsoft Store from one place, through the official APIs: listings,
screenshots, builds, releases, reviews. Every write is diffed and dry-run first, checked against your role, audited, and
you can watch it reach the store live. An MCP endpoint lets Claude (or any MCP client) do what the web app does, with
the same checks.

> **Status:** under active development, not released. See [What works today](#what-works-today).

## What it does

- **Three stores, one list.** A storeasy app pairs its App Store Connect, Google Play and Microsoft Store records.
  Store credentials are added per organization and sealed at rest.
- **Diff and dry run.** Every change becomes a change plan: storeasy reads the store live, shows the field level diff,
  and writes nothing until someone with the right role approves. Submitting for review and production releases also
  need a typed confirmation (app name and version).
- **Live activity.** Store work runs as jobs; every step (upload, commit, processing, reorder) is streamed over SignalR
  and kept as a replayable timeline.
- **Readiness check.** "What is missing before I can submit?" answered from one live read, the app's profile and the
  rules catalog.
- **MCP.** Remote MCP at `/mcp` with a personal access token, plus a local bridge (`storeasy-mcp`) for tools that take
  local files. See [docs/mcp.md](docs/mcp.md).
- **CI intake.** Builds go from CI straight to storeasy (GitHub Action with OIDC, Azure DevOps task, CLI, EAS and
  generic webhooks), which checks them and delivers them to a test channel. Production is never automatic. See
  [docs/ci-intake.md](docs/ci-intake.md).
- **Studio.** Store-ready screenshots from raw captures: captions per language, backgrounds, device frames drawn by
  storeasy, export to every required size.
- **Rules catalog.** Store requirements (screenshot sizes, SDK minimums, deadlines) are versioned data in
  [`rules/`](rules/README.md), shared by the readiness check, the studio and MCP. See
  [docs/rules-catalog.md](docs/rules-catalog.md).

## What works today

| Area | State |
|---|---|
| Accounts, organizations, roles, invitations, audit log | Works |
| Encrypted App Store Connect and Google Play connections, app and build sync | Works |
| Change plans (listings, screenshots, previews, categories, age rating, pricing, review info, builds, releases, submission) | Works (API) |
| Readiness check, rules catalog, compliance deadlines | Works (API) |
| Store jobs and live activity (SignalR) | Works |
| Web app | Shell, sign-up and sign-in, invitations, connections, members, organization settings, audit log, live activity; app and content screens in progress |
| Remote MCP, personal access tokens, CI intake server side, webhooks | In progress (client tools in [`tools/`](tools/README.md) are ready against [`tools/CONTRACT.md`](tools/CONTRACT.md)) |
| Microsoft Store, studio, subscriptions and in-app purchases | Planned |

## Screenshots

_Screenshots will be added before the first release._

<!-- Planned: apps list, change plan diff, live activity, readiness check, studio export. -->

## Quick start (local development)

Requirements: .NET 11 SDK, Node 22+, Docker.

```bash
# 1. Dependencies: Redis, PostgreSQL, Seq and SeaweedFS (local S3 with the storeasy-local bucket)
cd storeasy-api
docker compose up -d

# 2. Local secrets (user secrets, never files in the repository)
dotnet user-secrets set "Storeasy:Encryption:MasterKey" "$(openssl rand -base64 32)" --project src/Storeasy.Api
dotnet user-secrets set "Milvasoft:Identity:Token:SymmetricPublicKey" "$(openssl rand -hex 32)" --project src/Storeasy.Api

# 3. API on http://localhost:5000 (documentation: /api/documentation)
dotnet run --project src/Storeasy.Api

# 4. Web app on http://localhost:5173 (second terminal; proxies /api and /hubs to the API)
cd storeasy-app
npm install
cp .env.example .env
npm run dev
```

Seed a development database from the API documentation page (`POST /api/v1.0/developer/database/seed`); see
[storeasy-api/README.md](storeasy-api/README.md#5-initial-data). To run the web app without a backend:
`VITE_MOCK_API=true npm run dev`.

To run storeasy on a server, follow [docs/self-host.md](docs/self-host.md).

## Architecture

```text
browser, MCP clients, CI
        |
  storeasy-web (nginx): SPA, /api, /hubs, /mcp on one origin
        |
  storeasy-api (.NET): REST, SignalR hub, store engine, job runner, MCP
        |            \
  PostgreSQL 17    Redis 7        S3 (SeaweedFS or AWS), presigned URLs only
        |
  App Store Connect API, Google Play Developer API, Microsoft Store submission API
```

| Folder | What |
|---|---|
| [`storeasy-api/`](storeasy-api/README.md) | .NET API: auth, organizations and roles, sealed store connections, store engine, change plans, jobs, SignalR, MCP |
| [`storeasy-app/`](storeasy-app/README.md) | React web app |
| [`tools/`](tools/README.md) | `storeasy` CLI, `storeasy-mcp` bridge, GitHub Action, Azure DevOps task |
| [`rules/`](rules/README.md) | Store rules catalog (JSON) and its validator |
| [`deploy/`](deploy/docker-compose.yml) | Self-host compose file and environment template |
| [`docs/`](docs/) | Guides; the product plan (`urun-plani.md`) and design files are in Turkish |

More in [docs/architecture.md](docs/architecture.md).

## Security model

- **Store credentials** (App Store Connect `.p8` keys, Play service account JSON, Microsoft Entra secrets) are sealed
  with AES-256-GCM envelope encryption: a data key per record, wrapped by a master key from the environment. No
  endpoint ever returns them; the app shows a fingerprint only (key id, last 4 characters of the issuer id, service
  account e-mail). Only the job runner and the verify command open them, in memory.
- **Organization isolation.** Every query is scoped to the organization; membership and role are checked per request,
  so a role change applies at once.
- **Roles.** Owner, Admin, Editor, Viewer. Editors change content and test tracks; production releases, review
  submission, connections and members need Admin or Owner.
- **Tokens.** Access tokens carry a revocation stamp (password change, lock or role change ends every session).
  Personal access tokens, CI tokens and invitation tokens are stored only as SHA-256 hashes. CI uses OIDC trusted
  publishing, so no long lived secret sits in CI.
- **Logs** redact bearer tokens, JWTs, PEM blocks, signed URL queries and `access_token` values.
- **Repository.** No secret is committed; CI runs a gitleaks scan on every push.

Report vulnerabilities privately, see [SECURITY.md](SECURITY.md).

## Documentation

| Guide | |
|---|---|
| [Self-hosting](docs/self-host.md) | Docker compose, configuration reference, backups, upgrades, master key |
| [Connecting stores](docs/connecting-stores.md) | Apple API key roles, Play service account, Microsoft Entra app |
| [MCP](docs/mcp.md) | Remote MCP, local bridge, Claude Code and Claude Desktop setup |
| [CI intake](docs/ci-intake.md) | GitHub Action, Azure DevOps task, CLI, webhooks, delivery rules |
| [Rules catalog](docs/rules-catalog.md) | How store rules are kept and checked |
| [Architecture](docs/architecture.md) | Components, data flow, decisions |
| [Contributing](CONTRIBUTING.md) | Development setup, tests, conventions |

## License

storeasy is free software under the GNU Affero General Public License v3.0 only (AGPL-3.0-only), see
[LICENSE](LICENSE) and [NOTICE](NOTICE). If you modify storeasy and let users interact with it over a network, you must
offer those users the source code of your version (AGPL section 13); point the web app's "Source code" link
(`VITE_SOURCE_URL`) at it.

"App Store", "App Store Connect" and "Apple" are trademarks of Apple Inc.; "Google Play" is a trademark of Google LLC;
"Microsoft Store" is a trademark of Microsoft Corporation. storeasy is not affiliated with or endorsed by them.
