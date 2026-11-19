# storeasy

Manage App Store Connect, Google Play and Microsoft Store from one place, through the official APIs: listings,
screenshots, builds, releases, reviews. Every write is diffed and dry-run first, checked against your role, audited, and
you can watch it reach the store live. An MCP endpoint lets Claude (or any MCP client) do what the web app does, with
the same checks.

> **Status:** feature complete, not released. Everything below is built and tested against fake stores; real store
> calls have not been exercised end to end yet. See [What works today](#what-works-today).

## What it does

- **Three stores, one list.** A storeasy app pairs its App Store Connect, Google Play and Microsoft Store records.
  Store credentials are added per organization and sealed at rest. Work a store has no API for becomes an assisted
  step with a checklist and prepared answers.
- **Diff and dry run.** Every change becomes a change plan: storeasy reads the store live, shows the field level diff,
  and writes nothing until someone with the right role approves. Submitting for review and production releases also
  need a typed confirmation (app name and version).
- **Live activity.** Store work runs as jobs; every step (upload, commit, processing, reorder) is streamed over SignalR
  and kept as a replayable timeline.
- **Readiness check.** "What is missing before I can submit?" answered from one live read, the app's profile and the
  rules catalog.
- **Events.** App Store Connect webhooks, review inbox and replies, rejection cases, TestFlight, deadline reminders and
  outgoing webhooks; notifications in the app, by e-mail and as browser push.
- **Money.** Subscriptions, offers, prices per territory and in-app purchases in both stores, as change plans.
- **MCP.** Remote MCP at `/mcp` (142 tools) with a personal access token, plus a local bridge (`storeasy-mcp`) for
  tools that take local files. See [docs/mcp.md](docs/mcp.md).
- **CI intake.** Builds go from CI straight to storeasy (GitHub Action with OIDC, Azure DevOps task, CLI, EAS and
  generic webhooks), which checks them and delivers them to a test channel. Production is never automatic. See
  [docs/ci-intake.md](docs/ci-intake.md).
- **Studio.** Store-ready screenshots from raw captures: captions per language, backgrounds, device frames drawn by
  storeasy, export to every required size.
- **Rules catalog.** Store requirements (screenshot sizes, SDK minimums, deadlines) are versioned data in
  [`rules/`](rules/README.md), shared by the readiness check, the studio and MCP. See
  [docs/rules-catalog.md](docs/rules-catalog.md).

## What works today

Every area of the product plan (phases 1 to 8) is built. "Fakes only" means the store calls are covered by
integration tests against fake store servers but have not run against the real store yet.

| Area | State |
|---|---|
| Accounts (password and Google sign in, sessions, web push), organizations, seeded and custom roles, invitations, audit log | Built |
| Instance administration: users, platform roles, feature flags, registration mode, system status | Built |
| Store connections: App Store Connect, Google Play, Microsoft Store (Entra, secret expiry reminders) | Built; Apple and Google key roles checked live, store calls fakes only |
| App and build sync, readiness check, rules catalog, compliance deadlines, rule watcher | Built; store calls fakes only |
| Change plans: listings, screenshots, previews, categories, age rating, pricing, review info, builds, releases, rollouts, submission | Built; store calls fakes only |
| Microsoft Store: MSIX and MSI/EXE products, submissions, packages and flights, add-ons, installer checks | Built; fakes only |
| Subscriptions, offers and in-app purchases (both stores), purchase settings | Built; fakes only |
| Reviews, rejections, TestFlight, App Store Connect webhooks, outgoing webhooks, notifications | Built; store calls fakes only |
| Mockup studio: templates, device frames, captions per language, exports to screenshot plans | Built (caption translation suggestions need a provider; none ships) |
| Remote MCP (142 tools), personal access tokens, local bridge | Built |
| CI intake: OIDC trusted publishers, CI tokens, CLI, GitHub Action, Azure DevOps task, EAS and generic webhooks | Built; delivery fakes only, the App Store Connect build upload API needs one real build ([`External` mode](docs/ci-intake.md#apple-upload-modes) is the fallback) |
| Store jobs, live activity (SignalR), assisted steps | Built |
| Web app | Every area above has its screens; polishing continues |
| Self-host (Docker compose, SeaweedFS, health and status) | Built; see [docs/self-host.md](docs/self-host.md) |
| Public release, hosted version | Pending the owner's approval |

## Screenshots

_Screenshots will be added before the first release._

<!-- Planned: apps list, change plan diff, live activity, readiness check, studio export. -->

## Quick start (your own server)

storeasy is a tool you install and use with your team. Requirements: Docker with the Compose plugin, two DNS names
behind a TLS proxy (the app and its file storage) and an SMTP account for invitations.

```bash
git clone <repository url> storeasy && cd storeasy/deploy
cp .env.example .env          # fill in the addresses, the SMTP account and the generated secrets
docker compose up -d
docker compose logs storeasy-api | grep "setup code"
```

1. Open the site. While the server has no account it shows the **Setup** screen.
2. Enter the setup code from the log, your name, e-mail, password and the team name. You are signed in as the
   administrator and the owner of the team; the API prepared its database by itself.
3. Invite your team from **Members**, then connect your stores.

Details, TLS, backups and upgrades: [docs/self-host.md](docs/self-host.md). Hosting storeasy for many customers (open
sign-up, many organizations) is the advanced `STOREASY_MODE=MultiTenant` mode described there.

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

The development settings run the multi tenant mode (`Storeasy:Mode` in `appsettings.Development.json`; set
`SingleTeam` to try the first run setup). Initialize a development database with the bootstrap key (`POST /api/v1.0/developer/database/init`), sign in as
`rootuser` and seed the development users (`POST /api/v1.0/developer/database/seed`); see
[storeasy-api/README.md](storeasy-api/README.md#5-initial-data). The documentation and the developer endpoints are
open only with `MILVA_ENV=dev` (the launch profiles set it); never run `dev` or `test` on a public host. To run the web app without a backend:
`VITE_MOCK_API=true npm run dev`.

To run storeasy on a server, follow [docs/self-host.md](docs/self-host.md).

## Architecture

```text
browser, MCP clients, CI
        |
  storeasy-web (nginx): SPA, /api, /hubs, /mcp on one origin
        |
  storeasy-api (.NET): REST, SignalR hub, store engines, job runner, MCP, background senders
        |            \
  PostgreSQL 17    Redis 7        S3 (SeaweedFS or AWS), presigned URLs only
        |
  App Store Connect API, Google Play Developer API, Microsoft Store submission API
```

| Folder | What |
|---|---|
| [`storeasy-api/`](storeasy-api/README.md) | .NET API: auth, organizations and roles, sealed store connections, store engines, change plans, jobs, intake, events, studio, SignalR, MCP |
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
- **Roles.** Owner, Admin, Editor, Viewer, plus custom roles an organization defines from the same permissions (never
  more than the member who assigns them holds). Editors change content and test tracks; production releases, review
  submission, connections, webhooks and members need Admin or Owner by default.
- **Tokens and sessions.** Access tokens carry a revocation stamp (password change, lock or role change ends every
  session); users see their sessions and can end them. Personal access tokens, CI tokens, upload tokens and invitation
  tokens are stored only as SHA-256 hashes. CI uses OIDC trusted publishing, so no long lived secret sits in CI.
- **Outgoing calls** to user supplied addresses (webhooks, browser push, package downloads) reach public addresses
  only.
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
| [REST API](docs/api.md) | Areas, authentication, permissions; the full reference is the OpenAPI document |
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
