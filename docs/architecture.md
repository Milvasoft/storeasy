# Architecture

## Components

| Component | Technology | Role |
|---|---|---|
| `storeasy-api` | .NET 11, ASP.NET Core, EF Core (PostgreSQL), MediatR, SignalR, ModelContextProtocol, SkiaSharp | REST API, identity (password and Google sign in), organizations with seeded and custom roles, sealed store connections, store engines (App Store Connect, Google Play, Microsoft Store), change plans, job runner, package intake, webhooks and notifications (in-app, e-mail, web push), mockup studio renderer, live activity hub, remote MCP |
| `storeasy-app` | React 18, Vite, TypeScript, TanStack Query, Tailwind, SignalR client | Web app, served by nginx, which also proxies the API on the same origin |
| PostgreSQL 17 | | All product data, job queue, e-mail outbox, webhook logs, audit log |
| Redis 7 | | Cache, token revocation stamps, feature flags, rate limits, Google sign in state, SignalR backplane |
| S3 compatible storage | SeaweedFS (self-host) or AWS S3 | Screenshots, previews, mockup exports, packages; private bucket, presigned URLs only |
| `tools/` | Node 22, TypeScript | `storeasy` CLI, `storeasy-mcp` bridge, GitHub Action, Azure DevOps task |
| `rules/` | JSON + JSON Schema | Store rules catalog |

The API follows an onion layout: `Domain`, `Application` (CQRS features, one folder per use case), `Infrastructure`
(persistence, store gateways, storage, jobs), `Api` (controllers, hub, startup). Architecture tests enforce the layer
rules; see [storeasy-api/README.md](../storeasy-api/README.md#architecture-tests).

## Request path

```text
browser ---- https://storeasy.example.com ----+
MCP client -- /mcp --------------------------+--> nginx (storeasy-web)
CI runner --- /api/v1.0/... -----------------+       |  /api, /hubs (WebSocket), /mcp
stores, EAS -- /api/webhooks/... ------------+       |
                                                     v
                                               storeasy-api :8080
                                                     |
                     +---------------+---------------+------------------+
                     |               |               |                  |
                PostgreSQL         Redis       S3 (presigned)    App Store Connect,
                                                                 Google Play,
                                                                 Microsoft Store APIs
```

One origin means no CORS setup and one certificate. Files never pass through the API: clients get presigned URLs
and talk to the S3 endpoint directly.

## A write, end to end

Example: replacing the screenshots of one locale.

1. **Plan.** The web app (or an MCP tool) uploads the images to storage and calls the planning endpoint. The API reads
   the current set from the store, checks the files against the rules catalog (size, alpha, count), skips images whose
   MD5 is already in the set, and saves a pending change plan with the diff and a fingerprint of the touched store
   values. Nothing is written to the store.
2. **Approve.** A member with the plan's permission approves it (typed confirmation for submissions and production).
   The API reads the store again; if the fingerprinted values changed, it answers 409 and a new plan is needed.
   Otherwise it queues an `ApplyChangePlan` store job and writes the audit entry.
3. **Run.** `StoreJobRunner` (a hosted service inside the API) claims the job with `FOR UPDATE SKIP LOCKED`, opens the
   connection's credential in memory, and performs the store calls: reserve, upload, commit, wait for processing,
   reorder. Each call is a job step, published on `/hubs/activity` to the organization's group.
4. **Finish.** The job ends `Succeeded`, `Failed` (with retries for transient errors, exponential backoff) or
   `Cancelled`; one audit entry per changed field records who, when, what and through which channel (web, MCP, API).

## Key decisions

| Decision | Why |
|---|---|
| Store engine in C# inside the API, no separate worker | Credentials never leave the API process; events go straight to SignalR; self-host is one image |
| Organization roles checked per request from the membership (cached 60 s), not from token claims | A user has different roles in different organizations; a role change applies at once |
| Custom roles are sets of the same organization permissions, capped by the assigner | Teams can shape roles without a way to escalate past the person who grants them |
| E-mails through an outbox table | A slow or missing mail server never changes answer times or loses a mail |
| Jobs in PostgreSQL (`StoreJobs`) with leases | No extra queue service; instances never share a job; a crashed instance's job is reclaimed |
| Envelope encryption for credentials, master key from the environment | A database leak does not open the credentials; moving to a key vault only changes the key source |
| Change plans with store fingerprints | Nothing is written without a reviewed diff, and a plan never overwrites a change someone made in the store meanwhile |
| Rules as data (`rules/`) | Store requirements change faster than releases |
| Single origin (`/api`, `/hubs`, `/mcp` behind the web nginx) | No CORS, one certificate, simpler cookies and WebSockets |
| Remote MCP calls the same commands as the web app | One place for permission, diff, confirmation and audit |
| OIDC trusted publishing for CI | No long lived secret in CI |
| AGPL-3.0-only | Improvements to hosted versions stay open |

The full plan, with the reasoning behind each decision (K1 to K12), is in [urun-plani.md](urun-plani.md) (Turkish).

## Background work

Everything runs as hosted services inside the API process; each can be turned off per instance.

| Service | What |
|---|---|
| `StoreJobRunner` | Store jobs: syncs, change plan applies, package processing, review replies, TestFlight changes, Microsoft submissions, mockup exports |
| E-mail outbox sender | Sends queued e-mails (bodies sealed) with retries; requests never wait for a mail server |
| Web push sender | Encrypts and sends browser pushes with VAPID |
| Outgoing webhook sender | Signed deliveries to the organization's subscriptions, public addresses only, with retries |
| Schedules (cron, UTC) | Version state, review and TestFlight syncs, deadline reminders, rule watcher, retention cleanups |

Notifications are written once by a dispatcher that applies each user's per kind preferences and fans out to in-app,
e-mail, web push and outgoing webhooks. The schedules and cleanups are listed in
[self-host.md](self-host.md#11-retention-and-scheduled-work).

## Store engines

Each store sits behind the same gateway interfaces (read a snapshot, apply a change set), so change plans, readiness
and MCP do not care which store they talk to:

- **App Store Connect**: JSON:API with an ES256 JWT from the team key.
- **Google Play**: Android Publisher API with a service account token; every write is an edit that is committed or
  deleted.
- **Microsoft Store**: an Entra client credentials token for two APIs (MSIX submissions with flights and add-ons, and
  the MSI/EXE API); writes go into storeasy's own draft submission, and a content fingerprint detects drafts edited in
  Partner Center.
- **winget**: the GitHub REST API with the owner's token. Manifests are read from microsoft/winget-pkgs; the approved
  write pushes a branch with generated manifests to the owner's fork and opens a pull request, whose labels and checks
  are the review state and whose merge is the publication.

Channels after the first three plug in through `IStoreChannel` (connections, sync, store reads), a content gateway, a
change plan extension, a readiness contributor and their own jobs, with one row in `StoreChannels`; see
[adding-a-store.md](adding-a-store.md).

Work a store has no API for is an **assisted step**: a checklist, prepared answers and a hand-off prompt for a browser
agent in the member's own session. storeasy never stores a store password.

The engines are covered by integration tests against fake App Store Connect, Google Play, Microsoft Store and GitHub servers;
real store calls are not yet exercised end to end.

## Live activity

`/hubs/activity` (SignalR, Redis backplane) pushes job and step events to the organization's group. Clients
authenticate with the access token (`access_token` query value for WebSockets, redacted from logs) and join an
organization; the hub checks the membership. The web app animates outbound and inbound steps and falls back to a
status list with reduced motion.

## Security boundaries

- Every query is scoped by `OrganizationId`; integration tests check that another organization's record answers 404.
- One permission check for every channel: web, REST with a personal access token, MCP and CI tokens all pass the same
  organization access behaviour.
- Deny by default: every endpoint needs an authenticated user unless marked anonymous; anonymous endpoints are rate
  limited.
- Access tokens carry a revocation stamp checked on every request.
- Personal access tokens, CI tokens, upload tokens, invitation and account tokens are stored as SHA-256 hashes;
  revoked tokens are refused at once through a revocation list.
- Outgoing calls to user supplied addresses (webhooks, web push, package downloads, installer checks) are limited to
  public addresses, checked again at connection time.
- Logs redact bearer tokens, JWTs, PEM blocks, signed URL queries and `access_token` values.

See [SECURITY.md](../SECURITY.md) for reporting vulnerabilities.
