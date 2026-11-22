# Self-hosting storeasy

This guide runs storeasy on one Linux server with Docker: the API, the web app, PostgreSQL 17, Redis 7 and SeaweedFS
(S3 compatible object storage). Everything is in [`deploy/docker-compose.yml`](../deploy/docker-compose.yml) and
[`deploy/.env.example`](../deploy/.env.example).

storeasy installs like any self-hosted tool: one team, its own server, no developer steps.

1. Fill in `deploy/.env` ([section 3](#3-install)) and run `docker compose up -d`. The API prepares its database by
   itself.
2. Open the site. While the server has no account it shows the **Setup** screen.
3. Copy the one time setup code from the API log: `docker compose logs storeasy-api | grep "setup code"`.
4. Enter the code, your name, e-mail, password and the team name. You are signed in as the administrator and the owner
   of the team.
5. Invite your team from **Members**.

This is the default mode, `STOREASY_MODE=SingleTeam`. A hosted instance for many customers (open sign-up, many
organizations, a separate platform administrator) is the advanced `MultiTenant` mode, see
[section 5.2](#52-multi-tenant-mode-advanced).

## Contents

1. [What runs where](#1-what-runs-where)
2. [Requirements](#2-requirements)
3. [Install](#3-install)
4. [Reverse proxy and TLS](#4-reverse-proxy-and-tls)
5. [First admin and first account](#5-first-admin-and-first-account)
6. [Configuration reference](#6-configuration-reference)
7. [Master key: backup and rotation](#7-master-key-backup-and-rotation)
8. [Upgrades and migrations](#8-upgrades-and-migrations)
9. [Backups](#9-backups)
10. [Health, status and logs](#10-health-status-and-logs)
11. [Retention and scheduled work](#11-retention-and-scheduled-work)
12. [License and network use (AGPL)](#12-license-and-network-use-agpl)
13. [Known limits](#13-known-limits)

## 1. What runs where

```text
browser / CI / MCP client
        |
  TLS reverse proxy (yours: Caddy, Traefik, nginx)
        |                                   |
  storeasy-web (nginx, port 80)        seaweedfs (S3 API, port 8333)
   /          SPA                           ^
   /api/      -> storeasy-api:8080          | presigned PUT / GET (browsers, CI runners, the API)
   /hubs/     -> storeasy-api:8080 (WebSocket)
   /mcp       -> storeasy-api:8080 (streamable HTTP)
   /webhooks/ -> storeasy-api:8080
   /health/   -> storeasy-api:8080
                    |
          postgres:5432   redis:6379
```

- **One origin.** The web container serves the app and proxies `/api`, `/hubs`, `/mcp`, `/webhooks` and `/health` to
  the API, so there is no CORS setup and one certificate for the app. Inbound webhooks (App Store Connect, EAS, signed
  webhooks) live under `/api/webhooks/...` (the EAS and artifact receivers also under `/webhooks/...`) and pass the
  same way.
- **Files never pass through the API.** Uploads and downloads use presigned URLs on the S3 endpoint, so the S3 API needs
  its own public host name (for example `files.storeasy.example.com`).
- **Background work runs inside the API**: the store job runner, the e-mail outbox sender, the web push sender, the
  outgoing webhook sender and the scheduled syncs and cleanups (hosted services). No separate worker container.
- **Redis** holds the cache, token revocation stamps, feature flags, rate limit counters, the Google sign in state and
  the SignalR backplane. Losing it loses nothing permanent.

## 2. Requirements

- Linux server with Docker Engine 24+ and the Compose plugin. 2 vCPU and 4 GB RAM are enough for a team.
- Two DNS names pointing at the server: the app (`storeasy.example.com`) and the S3 endpoint
  (`files.storeasy.example.com`).
- An SMTP account for verification, password reset, invitation and notification e-mails.
- Outbound HTTPS to the store APIs (`api.appstoreconnect.apple.com`, `androidpublisher.googleapis.com`,
  `www.googleapis.com`, `oauth2.googleapis.com`, `login.microsoftonline.com`, `manage.devcenter.microsoft.com`,
  `api.store.microsoft.com`) and, if you use them, to Google sign in and the browser push services.
- `openssl` (or PowerShell) to generate secrets.
- A clone of this repository (the compose file builds the images from it).

## 3. Install

```bash
git clone <repository url> storeasy
cd storeasy/deploy
cp .env.example .env
chmod 600 .env
```

Fill in `.env`. The required values and how to create them:

| Key | How to create |
|---|---|
| `STOREASY_BASE_URL` | Public address of the app, for example `https://storeasy.example.com` |
| `STOREASY_MASTER_KEY` | `openssl rand -base64 32` |
| `STOREASY_TOKEN_KEY` | `openssl rand -hex 32` |
| `POSTGRES_PASSWORD` | `openssl rand -hex 32` |
| `S3_ACCESS_KEY` | `openssl rand -hex 16` |
| `S3_SECRET_KEY` | `openssl rand -hex 32` |
| `S3_PUBLIC_URL` | Public address of the S3 endpoint, for example `https://files.storeasy.example.com` |
| `SMTP_HOST`, `SMTP_FROM` (+ `SMTP_USERNAME`, `SMTP_PASSWORD`) | Your mail provider |

Optional: `STOREASY_GOOGLE_CLIENT_ID` and `STOREASY_GOOGLE_CLIENT_SECRET` ([Sign in with Google](#sign-in-with-google)),
`STOREASY_WEBPUSH_*` ([browser notifications](#browser-notifications-web-push)), `STOREASY_APPLE_UPLOAD_MODE`
([package intake](#package-intake)) and `STOREASY_COMMIT` (shown on the system status page).

PowerShell equivalents: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` and
`[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`.

Store `STOREASY_MASTER_KEY` in a password manager now, outside the server (see
[section 7](#7-master-key-backup-and-rotation)).

Then:

```bash
STOREASY_COMMIT=$(git rev-parse --short HEAD) docker compose up -d --build
docker compose ps
```

(`STOREASY_COMMIT` can also be set in `.env`; it is optional. Compose passes it to the API image build as the
`STOREASY_COMMIT` build argument, which stamps it into the image as `Storeasy__Build__Commit` and the assembly's
informational version, and also sets it at runtime. A prebuilt image keeps the commit it was built with when
`STOREASY_COMMIT` is empty. Building the image by hand:
`docker build -f storeasy-api/Dockerfile --build-arg STOREASY_COMMIT=$(git rev-parse --short HEAD) -t storeasy-api .`)

Startup order: PostgreSQL and Redis become healthy, `seaweedfs-config` writes the S3 identity from `.env` into a
volume, SeaweedFS starts, `seaweedfs-init` creates the bucket and exits, then the API starts, applies the database
migrations and creates the data it needs (roles, permissions, languages) when it is missing, then the web container.
This is idempotent and safe when several API instances start at once (they wait for each other on a PostgreSQL
advisory lock). Continue with [section 5](#5-first-admin-and-first-account).

### Images and the rules catalog

Both images are built from this checkout:

- **API** (`storeasy-api/Dockerfile`): the build context is the **repository root**, so the store rules catalog
  (`rules/`) is copied next to the application and embedded in it; the build fails if `rules/catalog.json` is missing.
  The runtime image is chiseled (no shell) and sets `MILVA_ENV=prod`. To build it by hand, from the repository root:
  `docker build -f storeasy-api/Dockerfile -t storeasy-api .`
- **Web** (`storeasy-app/Dockerfile`): the build context is `storeasy-app/`; nginx serves the app and proxies the API.

The API validates the catalog at startup and stops if it is invalid. To use another catalog without rebuilding, mount a
folder and point `Storeasy__Stores__RulesPath` at it (the commented lines in the compose file):

```yaml
    environment:
      Storeasy__Stores__RulesPath: /rules
    volumes:
      - ../rules:/rules:ro
```

To use prebuilt images instead of building, set `STOREASY_API_IMAGE` and `STOREASY_WEB_IMAGE` in `.env` and run
`docker compose up -d --no-build`.

## 4. Reverse proxy and TLS

The stack binds the web container to `127.0.0.1:${WEB_PORT}` (default 8080) and SeaweedFS to `127.0.0.1:${S3_PORT}`
(default 8333). Put a TLS terminating reverse proxy in front of both. Caddy example:

```caddyfile
storeasy.example.com {
	reverse_proxy 127.0.0.1:8080
}

files.storeasy.example.com {
	request_body {
		max_size 128MB
	}
	reverse_proxy 127.0.0.1:8333
}
```

Rules for any proxy:

- **App host:** pass WebSocket upgrades (`/hubs/activity`) and do not buffer `/mcp` responses (both are long lived; the
  inner nginx already allows one hour). Set `X-Forwarded-For` to the client address. The API trusts the last
  `Storeasy__Network__ForwardedHops` `X-Forwarded-For` hops (default 2: your proxy and the inner nginx). Run exactly
  one proxy in front of the web container, or set the value to your real chain (1 with nothing in front, 3 with a CDN
  plus a proxy). A value larger than the real chain lets clients spoof their address and escape rate limits. Never
  expose the web or API container directly. The app host must be reachable from the internet if you use App
  Store Connect webhooks, EAS or signed webhooks (they call `/api/webhooks/...`).
- **S3 host:** keep the original `Host` header (presigned URLs are signed for it), allow request bodies of at least
  64 MiB (the largest upload part) and long timeouts. The API container must also reach `S3_PUBLIC_URL`: it signs URLs
  for that address and checks completed uploads there.
- **S3 CORS:** the web app uploads from the browser, so the S3 host must allow `PUT` and `GET` from `STOREASY_BASE_URL`
  and expose the `ETag` header (the browser reads it after every part of a multipart upload). The bundled SeaweedFS
  does this itself: the compose file starts it with `-s3.allowedOrigins=${STOREASY_BASE_URL}`, and SeaweedFS (4.51
  checked) answers the preflight and sends `Access-Control-Expose-Headers: ETag, ...` on every response. Do not add CORS
  headers at the proxy as well (duplicate `Access-Control-Allow-Origin` headers make the browser refuse the response);
  only make sure the proxy passes the `Origin` request header and the `Access-Control-*` response headers through.
  For AWS S3 or another S3 service, set the bucket rule instead. With the AWS CLI:

  ```bash
  cat > cors.json <<'EOF'
  { "CORSRules": [ { "AllowedOrigins": ["https://storeasy.example.com"], "AllowedMethods": ["GET", "PUT", "HEAD"],
      "AllowedHeaders": ["*"], "ExposeHeaders": ["ETag"], "MaxAgeSeconds": 3600 } ] }
  EOF
  AWS_ACCESS_KEY_ID=<S3_ACCESS_KEY> AWS_SECRET_ACCESS_KEY=<S3_SECRET_KEY> \
    aws s3api put-bucket-cors --endpoint-url "$S3_PUBLIC_URL" --bucket storeasy --cors-configuration file://cors.json
  ```

  If you replace SeaweedFS with a service that has no CORS support at all, answer the preflight at the proxy for the S3
  host instead (nginx: on `OPTIONS` return 204 with `Access-Control-Allow-Origin: <STOREASY_BASE_URL>`,
  `Access-Control-Allow-Methods: GET, PUT, HEAD`, `Access-Control-Allow-Headers: *`, and add
  `Access-Control-Allow-Origin` and `Access-Control-Expose-Headers: ETag` to every other response with `always`).

## 5. First admin and first account

> **Warning.** Run only `MILVA_ENV=prod` (the image default) on any server that something else can reach. `test` and
> `dev` are development settings and must never run on a public host, not even for a moment: `dev` opens the API
> documentation and the developer endpoints (which can reset or seed the database), and `test` is not hardened for
> production (storeasy versions before this change opened the same features with `test`). The multi tenant steps
> below use `dev` only in a temporary container bound to `127.0.0.1`.

### 5.1 Single team (default)

While the server has no user, the API generates a random one time setup code at startup and writes it to its log once,
at warning level:

```bash
docker compose logs storeasy-api | grep "setup code"
# ... WRN] Storeasy setup code: ABCD-EFGH-JKMN
```

Open `STOREASY_BASE_URL`. The site shows the **Setup** screen (Kurulum) instead of the sign in. Enter the code, your
first and last name, e-mail, a password and the team name. In one step the API creates:

- your account, verified, as the administrator of the server (the System pages: status, users, languages, feature
  switches),
- the team (the single organization of the server) with you as its **Owner**,

and signs you in. The code is compared in constant time and the endpoint is rate limited; case, spaces and dashes do
not matter. Once a user exists the setup is closed for good (the endpoint answers 409) and the code is forgotten, so it
is harmless in old logs. The code keeps a freshly started server that is already reachable from being taken over by
the first visitor: only someone who can read the server's log can finish the setup.

Then invite your team from **Members**. There is no sign-up in this mode: people join through the invitation e-mail
(SMTP must work), and "Sign in with Google" works for invited and existing addresses when it is configured. The server
has exactly one organization, the team; creating another is refused. Everything lives in one **Settings** area: the
team, its members and roles, store connections and, for you, the System pages.

Automated installs can fix the code instead of reading the log: set `STOREASY_SETUP_CODE` (at least 12 characters) for
the first start and remove it afterwards; a fixed code is never written to the log.

**An install from before this mode** (initialized with `developer/database/init`, so it has `rootuser` and perhaps no
organization) keeps working: no setup screen appears because a user exists. Sign in as `rootuser`; without an
organization you go straight to a one step **Create your team** screen. Create it there, then invite the others.

### 5.2 Multi tenant mode (advanced)

For a hosted instance with many customers set `STOREASY_MODE=MultiTenant`. That mode keeps the original behaviour:
registration by `STOREASY_REGISTRATION_MODE` (`Open` by default), any number of organizations, an organization switcher
and a separate platform administrator, `rootuser`, created once with a developer step. The API applies the migrations
at startup but creates no data and shows no setup screen.

The API image runs with `MILVA_ENV=prod`, which closes the developer endpoints, including database initialization.
Initialize a new database once with a temporary development instance of the same image, on the server itself. The
anonymous `developer/database/init` endpoint opens only when `MILVA_ENV=dev` and a one-off bootstrap key
(`Storeasy__Bootstrap__InitKey`, at least 32 characters) are both set, and it requires that key in the
`X-Storeasy-Init-Key` header:

```bash
docker compose stop storeasy-api
export Storeasy__Bootstrap__InitKey="$(openssl rand -hex 32)"
# -e with only the name passes the key from this shell, so it never appears on a command line or in a file.
docker compose run -d --name storeasy-init -e MILVA_ENV=dev -e Storeasy__Bootstrap__InitKey   -p 127.0.0.1:5080:8080 storeasy-api
curl -fsS http://127.0.0.1:5080/api/v1.0/healthcheck/ready     # repeat until it answers
curl -fsS -X POST -H "X-Storeasy-Init-Key: $Storeasy__Bootstrap__InitKey"   http://127.0.0.1:5080/api/v1.0/developer/database/init
docker rm -f storeasy-init
unset Storeasy__Bootstrap__InitKey
docker compose up -d storeasy-api
```

1. `init` seeds the root user `rootuser` (the instance admin), roles, permissions and languages, and returns the
   generated root password once (`data` of the response). Store it in a password manager; it is never shown again.
   `init` refuses a database that is already initialized.
2. Without the key setting (or with a shorter one) the endpoint answers 404; with a missing or wrong header it answers
   401. It is rate limited and the header is never logged.
3. Stop the temporary container right away and never keep the key: do not write it into `.env`, the compose file or
   any other file. The normal container (`MILVA_ENV=prod`) closes the endpoint whatever the key.

Then create your own account:

1. With registration `Open` (the default), register in the web app (or sign in with Google) and verify your e-mail
   (SMTP must work; creating an organization or a store connection needs a verified address).
2. Create your organization; you are its Owner.
3. Close registration. Sign in as `rootuser`, open the admin area's **System** page and set the registration mode to
   `InviteOnly` (people join through invitations you send) or `Closed`. The choice is saved in the database
   (`SystemSettings`) and wins over `STOREASY_REGISTRATION_MODE` until it is cleared, so no restart is needed. The same
   call over the API: `PUT /api/v1.0/system/settings/registration-mode` with `{"mode": "InviteOnly"}`; `null` returns
   to the configured mode.

The root user is for instance administration (users and their locks, platform roles, feature flags, languages, the
rule watcher, system status and settings), not for daily store work.

## 6. Configuration reference

The API reads `appsettings.json` and then environment variables; a variable name is the key with `:` replaced by `__`
(`Storeasy:Encryption:MasterKey` becomes `Storeasy__Encryption__MasterKey`). Values marked **secret** must only come
from the environment (or user secrets on a developer machine), never from a file in the repository. The `.env` column
names the variable of [`deploy/.env.example`](../deploy/.env.example) that the compose file maps to the key; keys
without one keep their default unless you add them to the API's `environment` (or an override file).

### Core

| Key | `.env` | Default | Description |
|---|---|---|---|
| `ConnectionStrings:DefaultConnectionString` | `POSTGRES_*` | empty | **Secret.** PostgreSQL 17 connection string. |
| `Milvasoft:Caching:Redis:ConnectionString` | | `redis` | Redis 7 as `host:port`. The API starts while Redis is down (readiness answers 503, rate limits fail open, Google sign in is unavailable). |
| `CacheKeys:Prefix` | `STOREASY_CACHE_PREFIX` | `storeasy` | Prefix of every Redis key and SignalR channel. Use a different one per instance when several share one Redis. |
| `Milvasoft:Identity:Token:SymmetricPublicKey` | `STOREASY_TOKEN_KEY` | empty | **Secret.** HMAC-SHA256 key for access tokens, at least 32 characters. Missing or short: the API stops at startup outside Development. |
| `Milvasoft:Identity:Token:ExpirationMinute` | | `30` | Access token lifetime. The refresh token lives 48 times as long (24 hours by default). |
| `Milvasoft:Identity:Token:TokenValidationParameters:ValidIssuer`, `ValidAudience` | | `Storeasy` | Issuer and audience written into and required on tokens. Change both together. |
| `Milvasoft:Identity:Password:*` | | 6 characters, upper, lower, digit, symbol | Password policy. |
| `Milvasoft:Identity:Lockout:MaxFailedAccessAttempts`, `DefaultLockoutTimeSpan` | | `5`, `5` | Sign in lockout (minutes). |
| `Milvasoft:Interception:Log:LogDefaultParameters` | | `false` | Logs every argument and result of `[Log]` methods. Keep `false` in production: they can contain personal data. |
| `Milvasoft:FileOperations:Json:EncryptionKey` | | empty | **Secret.** Not used by storeasy today. |
| `MILVA_ENV` | | `prod` in the image | `dev`, `test` or `prod`; selects the log sinks. The developer endpoints and the API documentation are open only with exactly `dev`; every other value (`test`, `prod`, empty, unknown) closes them. Use `prod` on every reachable server: `test` and `dev` must never run on a public host. |
| `Storeasy:Mode` | `STOREASY_MODE` | `SingleTeam` | `SingleTeam`: one team on its own server; the API prepares the database, the first visitor with the setup code creates the administrator and the team, one organization, no sign-up ([section 5.1](#51-single-team-default)). `MultiTenant`: a hosted instance for many customers ([section 5.2](#52-multi-tenant-mode-advanced)). |
| `Storeasy:Bootstrap:SetupCode` | `STOREASY_SETUP_CODE` | empty | **Secret.** Single team only: a fixed first run setup code (at least 12 characters) for automated installs. Empty: a random code is generated and logged once while the server has no user. Remove it after the setup. |
| `Storeasy:Bootstrap:InitKey` | | empty | **Secret.** Multi tenant only: one-off key that opens `developer/database/init` (only with `MILVA_ENV=dev`, at least 32 characters, sent as `X-Storeasy-Init-Key`). Set it only on the temporary container of [section 5.2](#52-multi-tenant-mode-advanced), never keep it. |
| `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS` | | `Production`, `http://+:8080` in the image | Leave as is. |

The remaining `Milvasoft:*`, `ApiHost` and `HealthChecksUI` keys in `appsettings.json` are library settings that need
no change.

**Sessions.** Every sign in is a session the user sees under **Account** (browser, address, last use) and can end, one
by one or all others at once. A password change or reset, a lock by the instance admin and a role change end the
affected sessions at once (revocation stamp in Redis, checked on every request).

### Accounts, registration and e-mail

| Key | `.env` | Default | Description |
|---|---|---|---|
| `Storeasy:App:BaseUrl` | `STOREASY_BASE_URL` | empty | Public address of the web app. E-mail links point at `{BaseUrl}/verify-email`, `/reset-password` and `/invitation`; Google redirects to `{BaseUrl}/auth/google/callback`. Required outside Development. |
| `Storeasy:Registration:Mode` | `STOREASY_REGISTRATION_MODE` | `Open` | Multi tenant only (a single team server is always `InviteOnly`): `Open`, `InviteOnly` (a valid invitation for the same e-mail is required) or `Closed`. A mode saved on the admin **System** page wins over this value (see [section 5.2](#52-multi-tenant-mode-advanced)). |
| `Storeasy:Email:Provider` | `STOREASY_EMAIL_PROVIDER` | `Smtp` | `Smtp` (self-host) or `Notifike` (Milvasoft hosting). `Log` writes e-mails to the log and is refused outside Development. |
| `Storeasy:Email:FromName` | `STOREASY_EMAIL_FROM_NAME` | `storeasy` | Sender display name. |
| `Storeasy:Email:Smtp:Host`, `Port`, `UserName`, `From`, `EnableSsl` | `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_FROM`, `SMTP_ENABLE_SSL` | empty, `587`, empty, empty, `true` | SMTP server (`EnableSsl` upgrades with STARTTLS). `Host` and `From` are required with `Smtp`; otherwise the API stops at startup and names the missing key. |
| `Storeasy:Email:Smtp:Password` | `SMTP_PASSWORD` | empty | **Secret.** SMTP password. |
| `Storeasy:Email:Notifike:BaseUrl`, `ApiKey` | `NOTIFIKE_BASE_URL`, `NOTIFIKE_API_KEY` | empty | Notifike API (`ApiKey` is **secret**). Both required with `Notifike`. |
| `Storeasy:Email:Outbox:Enabled` | `STOREASY_EMAIL_OUTBOX_ENABLED` | `true` | Whether this instance runs the background e-mail sender. E-mails are never sent on the request path: they are queued in the `EmailOutbox` table (body sealed with the master key) and sent from there, so a slow provider or a missing account does not change answer times. Every instance can queue. |
| `Storeasy:Email:Outbox:PollIntervalMilliseconds`, `MaxAttempts`, `RetryBaseDelaySeconds`, `LeaseSeconds` | | `1000`, `6`, `30`, `120` | Sender pause, attempts of one e-mail (then it is dropped and logged), doubling retry delay (at most an hour), claim lease. |
| `Storeasy:Auth:Google:ClientId`, `ClientSecret` | `STOREASY_GOOGLE_CLIENT_ID`, `STOREASY_GOOGLE_CLIENT_SECRET` | empty | Sign in with Google (`ClientSecret` is **secret**). Off while either is empty. See [Sign in with Google](#sign-in-with-google). |
| `Storeasy:Auth:Google:Authority`, `StateLifetimeMinutes` | | `https://accounts.google.com`, `10` | OpenID Connect issuer (https only; change it only for testing) and how long a started sign in may take. |
| `Storeasy:WebPush:PublicKey`, `PrivateKey` | `STOREASY_WEBPUSH_PUBLIC_KEY`, `STOREASY_WEBPUSH_PRIVATE_KEY` | empty | VAPID key pair of browser notifications (`PrivateKey` is **secret**). Off while either is empty. See [Browser notifications](#browser-notifications-web-push). |
| `Storeasy:WebPush:Subject`, `TimeToLiveSeconds` | `STOREASY_WEBPUSH_SUBJECT` | `mailto:admin@localhost`, `86400` | Contact the push services may use (`mailto:` or https) and how long they keep an undelivered push. |

#### Sign in with Google

1. In Google Cloud Console create an OAuth client of type **Web application** (APIs and Services, Credentials). The
   consent screen needs only the `openid`, `email` and `profile` scopes.
2. Add the authorized redirect URI `<STOREASY_BASE_URL>/auth/google/callback` (the web app's callback page; it sends
   the code and state to `POST /api/v1.0/account/login/google`).
3. Set `STOREASY_GOOGLE_CLIENT_ID` and `STOREASY_GOOGLE_CLIENT_SECRET` in `.env` and run
   `docker compose up -d storeasy-api`. The sign in page shows the Google button once
   `GET /api/v1.0/system/public-settings` reports it on.

The flow is the authorization code flow with PKCE; the code verifier and nonce never leave the API (Redis, ten minutes,
single use), so Redis is required for it. An account is linked only by an address Google reports as verified; an
existing account that never verified its address is taken over by that proof (its password is replaced with a random
one and its sessions end). New accounts follow the registration mode: `InviteOnly` needs an invitation for the same
address, `Closed` creates none (existing accounts can still sign in). No Google call is made while the client id or
secret is empty.

#### Browser notifications (web push)

Pushes go straight from the API to the browsers' push services with VAPID (RFC 8292) and the payload encrypted to the
browser (RFC 8291); no third party account or library is involved. Generate one P-256 key pair per instance:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out vapid.pem
# Public key: the 65 byte uncompressed point, base64url
openssl ec -in vapid.pem -pubout -outform DER 2>/dev/null | tail -c 65 | base64 | tr '/+' '_-' | tr -d '=\n'; echo
# Private key: the 32 byte scalar, base64url
openssl ec -in vapid.pem -outform DER 2>/dev/null | tail -c +8 | head -c 32 | base64 | tr '/+' '_-' | tr -d '=\n'; echo
```

Put them into `STOREASY_WEBPUSH_PUBLIC_KEY` and `STOREASY_WEBPUSH_PRIVATE_KEY`, set `STOREASY_WEBPUSH_SUBJECT` to a
contact address, run `docker compose up -d storeasy-api` and delete `vapid.pem`. The web app reads the public key from
`GET /api/v1.0/system/public-settings`; users turn notifications on per browser under **Account** and choose the kinds
per channel in their notification preferences. Changing the key pair invalidates every browser subscription (users
subscribe again). The API sends pushes only to public addresses (the same guard as outgoing webhooks), so the server
needs outbound HTTPS to the push services (`fcm.googleapis.com`, `updates.push.services.mozilla.com`,
`*.notify.windows.com`, `web.push.apple.com`).

### Store connections, jobs and the store engine

| Key | `.env` | Default | Description |
|---|---|---|---|
| `Storeasy:Encryption:MasterKey` | `STOREASY_MASTER_KEY` | empty | **Secret.** Base64 of 32 random bytes. Seals store credentials, saved demo passwords, webhook and pull source secrets and queued e-mails (AES-256-GCM envelope encryption). |
| `Storeasy:Encryption:KeyVersion` | `STOREASY_MASTER_KEY_VERSION` | `1` | Version of the current master key. Raise it when rotating. |
| `Storeasy:Encryption:RetiredKeys:<version>` | | none | **Secret.** Older master keys by version, so records sealed before a rotation still open. |
| `Storeasy:Jobs:Enabled` | `STOREASY_JOBS_ENABLED` | `true` | Whether this instance runs store jobs. Every instance can queue them. |
| `Storeasy:Jobs:Workers` | `STOREASY_JOBS_WORKERS` | `4` | Jobs one instance runs at the same time. |
| `Storeasy:Jobs:PollIntervalMilliseconds` | | `2000` | Pause of an idle worker. |
| `Storeasy:Jobs:LeaseSeconds` | | `300` | Lease of a running attempt, renewed on every step; an expired lease lets another worker reclaim the job. |
| `Storeasy:Jobs:DefaultMaxAttempts` | | `3` | Attempts of a job. |
| `Storeasy:Jobs:RetryBaseDelaySeconds`, `RetryMaxDelaySeconds` | | `30`, `900` | Exponential backoff between attempts. |
| `Storeasy:Stores:MaxRetries` | | `3` | Retries of an App Store Connect or Google Play call answered with 429 or 5xx. |
| `Storeasy:Stores:RetryBaseDelayMilliseconds`, `RetryMaxDelaySeconds` | | `800`, `60` | Backoff of those retries; a `Retry-After` header wins. |
| `Storeasy:Stores:AssetPollIntervalMilliseconds`, `AssetPollTimeoutSeconds` | | `2000`, `120` | Wait for Apple to process an uploaded screenshot. |
| `Storeasy:Stores:UploadChunkBytes` | | `8388608` | Chunk size of resumable Play bundle uploads (a multiple of 256 KiB). |
| `Storeasy:Stores:PlanLifetimeMinutes` | | `2880` | How long a pending change plan can be approved (48 hours). |
| `Storeasy:Stores:RulesPath` | | empty | Folder with `catalog.json` and the store files. Empty: the copy next to the application, then the embedded copy. |
| `Storeasy:MicrosoftStore:LoginBaseUrl` | | `https://login.microsoftonline.com` | Microsoft Entra authority (token endpoint `{LoginBaseUrl}/{tenantId}/oauth2/v2.0/token`). |
| `Storeasy:MicrosoftStore:DevCenterBaseUrl`, `DevCenterScope` | | `https://manage.devcenter.microsoft.com/v1.0/my`, `https://manage.devcenter.microsoft.com/.default` | MSIX submission API and its scope. |
| `Storeasy:MicrosoftStore:StoreApiBaseUrl`, `StoreApiScope` | | `https://api.store.microsoft.com`, `https://api.store.microsoft.com/.default` | MSI/EXE submission API and its scope. |
| `Storeasy:MicrosoftStore:PartnerCenterProductUrl` | | `https://partner.microsoft.com/dashboard/products/{productId}/overview` | Partner Center link of assisted steps. |
| `Storeasy:MicrosoftStore:MaxAttempts`, `RetryBaseDelayMilliseconds`, `MaxRetryDelaySeconds` | | `4`, `1000`, `60` | Attempts of one Microsoft call on 429, 5xx or a network error, and their backoff. |
| `Storeasy:MicrosoftStore:StatusPollIntervalMilliseconds`, `StatusPollAttempts` | | `5000`, `24` | Status reads after a commit before the job leaves the rest to the sync. |
| `Storeasy:MicrosoftStore:InstallerCheckMaxBytes`, `BlockPrivateDownloadAddresses` | | 4 GiB, `true` | Largest MSI/EXE installer the Authenticode check downloads, and the guard that refuses installer hosts on private addresses. |
| `Storeasy:Winget:ApiBaseUrl`, `Upstream`, `UpstreamBranch` | | `https://api.github.com`, `microsoft/winget-pkgs`, `master` | GitHub REST API and the repository winget pull requests go to. |
| `Storeasy:Winget:InstallerMaxBytes`, `BlockPrivateDownloadAddresses` | | 4 GiB, `true` | Largest installer downloaded for its SHA-256 when a winget plan is applied, and the guard that refuses installer hosts (and redirects) on private addresses. |
| `Storeasy:Winget:SearchLimit` | | `30` | Most pull requests a winget sync looks up per package. |
| `Storeasy:Chrome:ApiBaseUrl`, `TokenUrl` | | `https://chromewebstore.googleapis.com`, `https://oauth2.googleapis.com/token` | Chrome Web Store API and the token endpoint of OAuth client credentials (a service account key names its own). |
| `Storeasy:Chrome:ReviewPages`, `MetricsBackfillDays` | | `5`, `90` | Pages of 200 reviews a review sync reads, and the days the first metrics read goes back. |
| `Storeasy:Chrome:UploadPollAttempts`, `UploadPollIntervalMilliseconds` | | `10`, `3000` | Status reads after an upload the store is still processing, before a plan gives up (nothing is submitted then). |
| `Storeasy:Firefox:ApiBaseUrl`, `TokenLifetimeSeconds` | | `https://addons.mozilla.org/api/v5`, `60` | AMO API v5 and the lifetime of each signed request token (AMO allows at most 300 seconds). |
| `Storeasy:Firefox:RatingPages`, `VersionPages` | | `5`, `2` | Pages of 50 ratings a review sync reads, and pages of 50 versions a sync reads. |
| `Storeasy:Firefox:UploadPollAttempts`, `UploadPollIntervalMilliseconds` | | `20`, `3000` | Reads of an upload's validation while a version plan is applied, before the job is retried. |
| `Storeasy:Studio:FontsPath`, `PreviewMaxWidth`, `AssetCacheMegabytes` | | empty (`StudioFonts` next to the application), `800`, `128` | Mockup studio fonts (open licensed, shipped in the image), widest preview in pixels and the memory of the raw screenshot cache. |

#### Package intake

| Key | `.env` | Default | Description |
|---|---|---|---|
| `Storeasy:Intake:AppleUploadMode` | `STOREASY_APPLE_UPLOAD_MODE` | `Api` | `Api`: storeasy uploads IPAs with the App Store Connect build upload API. `External`: the CI uploads to Apple itself (EAS Submit, Xcode Cloud, Transporter); storeasy uploads nothing, waits for the build to appear in App Store Connect, then applies the delivery rule. See [ci-intake.md](ci-intake.md#apple-upload-modes). |
| `Storeasy:Intake:UrlLifetimeMinutes` | | `120` | Lifetime of the presigned part URLs of package uploads (at least 60). |
| `Storeasy:Intake:ProcessMaxAttempts` | | `40` | Attempts of a package processing job (an iOS delivery waits for Apple's processing by retrying). |
| `Storeasy:Intake:AbandonedUploadHours`, `PendingAssetHours` | | `48`, `48` | When unfinished package uploads and unconfirmed asset uploads are cleaned up. |
| `Storeasy:Intake:MaxRedirects` | | `5` | Redirects followed when downloading a package announced by EAS or a signed webhook. |

How long packages are kept (1 to 365 days, default 30) and the largest package (at most 4 GiB) are organization
settings in the web app (intake settings), not configuration.

### Events, webhooks and schedules

| Key | `.env` | Default | Description |
|---|---|---|---|
| `Storeasy:Events:SchedulesEnabled` | `STOREASY_SCHEDULES_ENABLED` | `true` | Whether this instance runs the scheduled syncs and reminders ([section 11](#11-retention-and-scheduled-work)). With several API instances keep it on in one. |
| `Storeasy:Webhooks:PublicBaseUrl` | | `Storeasy:App:BaseUrl` | Public address of the inbound webhook receivers (`{base}/api/webhooks/apple/{id}`, `/api/webhooks/signed/{id}`, `/api/webhooks/google/{id}`), as App Store Connect, Pub/Sub and other senders reach it. A Google Play push token must carry exactly this address as its audience. |
| `Storeasy:Webhooks:GooglePushIssuers`, `GooglePushKeysUrl` | | `https://accounts.google.com`, `accounts.google.com`; `https://www.googleapis.com/oauth2/v3/certs` | Issuers and key set of the Pub/Sub push tokens (Google Play notifications). Change only for tests. |
| `Storeasy:Webhooks:SecretGraceHours`, `MaxEventAgeHours`, `SignedToleranceSeconds` | | `24`, `168`, `300` | How long the previous secret works after a rotation, the oldest App Store Connect event still processed, and the clock difference accepted for signed webhooks. |
| `Storeasy:OrgWebhooks:Enabled` | | `true` | Whether this instance runs the outgoing webhook sender. |
| `Storeasy:OrgWebhooks:PollIntervalMilliseconds`, `MaxAttempts`, `RetryBaseDelaySeconds`, `TimeoutSeconds` | | `2000`, `6`, `30`, `10` | Outgoing webhook delivery: pause, attempts, doubling retry delay (at most an hour), timeout of one attempt. |
| `Storeasy:OrgWebhooks:AllowHttp`, `MaxSubscriptions` | | `false`, `20` | Plain HTTP targets (never in production; private addresses are refused regardless) and subscriptions per organization. |
| `Storeasy:RuleWatcher:Enabled`, `MaxBytes` | | `true`, 2 MiB | Whether the daily rule watcher runs and the largest page it reads. |
| `Storeasy:Ai:Model`, `MaxTokens`, `Effort`, `TimeoutSeconds` | | `claude-haiku-5-5`, `1024`, `low`, `60` | AI review reply drafts ([api.md](api.md#ai-reply-drafts)): the default Claude model (an organization may choose another in Settings, AI), its output token limit, effort and the timeout of one call. There is no instance key: each organization saves its own Anthropic API key in Settings, AI, sealed with `Storeasy:Encryption:MasterKey`. |
| `Storeasy:Ai:PreReviewModel`, `PreReviewMaxTokens`, `PreReviewEffort`, `PreReviewFallback`, `PreReviewMaxImages`, `PreReviewImageMaxEdge`, `PreReviewTimeoutSeconds` | | `claude-sonnet-5-5`, `16000`, per model (`low` Haiku, `medium` Sonnet and Opus), `true`, `10`, `1200`, `300` | AI pre-reviews ([api.md](api.md#ai-pre-review)): the default model, the output token limit, the effort by model id (`Storeasy__Ai__PreReviewEffort__claude-sonnet-5-5`), the server-side refusal fallback on models that have it, the screenshots sent and their longest edge in pixels, and the timeout of one call. Screenshots are downloaded from the stores' image hosts (`*.mzstatic.com`, `*.googleusercontent.com`, `*.ggpht.com`). |
| `Storeasy:RuleWatcher:AllowedHosts`, `Sources` | | Apple, Google and Microsoft developer domains; seven Apple and Google pages | Official hosts the watcher may fetch and the pages it watches (added to the database when missing). |

### Object storage

| Key | `.env` | Default | Description |
|---|---|---|---|
| `Storage:S3:Bucket` | `S3_BUCKET` | empty | Private bucket. Empty disables uploads (the endpoints answer 503). |
| `Storage:S3:Region` | `S3_REGION` | `eu-central-1` | Bucket region; for SeaweedFS any value works as the signing region. |
| `Storage:S3:AccessKey`, `SecretKey` | `S3_ACCESS_KEY`, `S3_SECRET_KEY` | empty | **Secret.** Bucket credentials. Without an access key the AWS default credential chain is used. |
| `Storage:S3:ServiceUrl` | `S3_PUBLIC_URL` | empty | S3 compatible endpoint (SeaweedFS). Empty for AWS S3. Must be reachable by browsers, CI runners and the API. |
| `Storage:S3:ForcePathStyle` | | `true` | Path style addressing (`host/bucket/key`), needed by SeaweedFS. Ignored without `ServiceUrl`. |
| `Storage:S3:PresignedUrlLifetimeMinutes` | | `30` | Lifetime of presigned URLs. |

AWS S3 works too: leave `ServiceUrl` empty, set the region, block public access on the bucket and add the CORS rule from
[section 4](#4-reverse-proxy-and-tls). SeaweedFS can then be removed from the compose file.

### Observability and build

| Key | `.env` | Default | Description |
|---|---|---|---|
| `SeqUri` | `SEQ_URI` | empty | Seq server (`MILVA_ENV` `test` or `prod`). Empty disables Seq. |
| `OpenTelemetry:CollectorUrl` | `OTEL_COLLECTOR_URL` | empty | OTLP gRPC endpoint for logs, metrics and traces (`prod`). Empty disables OpenTelemetry. |
| `OpenTelemetry:ServiceName` | | `storeasy-api` | Service name in logs, metrics and traces. |
| `Storeasy:Build:Commit` | `STOREASY_COMMIT` | empty | Source commit shown on the system status page. Empty: the commit stamped into the assembly version, if any (the Docker build stamps the `STOREASY_COMMIT` build argument). |

With both `SeqUri` and `OpenTelemetry:CollectorUrl` empty in `prod`, logs go to the console
(`docker compose logs storeasy-api`).

### Feature flags

Two runtime kill switches exist, managed by the instance admin (admin area, **Feature flags**). A switched off feature
answers 503 from the next request on; a missing flag reads as on.

| Flag | Covers |
|---|---|
| `Export` | Excel exports. |
| `Studio.AiCaptions` | Caption translation suggestions in the mockup studio. No translation provider ships with storeasy, so the suggestions answer 503 on a self-hosted instance either way. |

### Web app

| Key | When | Description |
|---|---|---|
| `VITE_SOURCE_URL` | build | "Source code" link in the footer and account menu (AGPL section 13). Empty: the upstream repository. In `.env`: `STOREASY_SOURCE_URL`. |
| `VITE_APP_VERSION` | build | Version shown in the menu. In `.env`: `STOREASY_VERSION`. |
| `VITE_API_URL` | build | API origin. Keep empty for the single origin setup. |
| `STOREASY_API_UPSTREAM` | runtime | Where nginx proxies `/api`, `/hubs`, `/mcp`, `/webhooks` and `/health` (default `http://storeasy-api:8080`). |

`VITE_*` values are baked into the image: changing one means `docker compose build storeasy-web`.

## 7. Master key: backup and rotation

The master key seals every store credential (App Store Connect keys, Play service accounts, Microsoft Entra secrets, GitHub tokens, AMO API keys),
saved demo passwords, the secrets of inbound webhook endpoints, outgoing webhook subscriptions and pull sources, and
queued e-mails. Each record gets its own data key; the master key wraps the data keys. A database dump alone does not
reveal any of them, and the key alone is useless without the database.

**Backup.** Keep `STOREASY_MASTER_KEY` and its version in a password manager or vault, separate from database backups.
If the key is lost, every store connection must be added again, saved demo passwords entered again and webhook and pull
source secrets replaced.

**Rotation:**

1. Generate a new key: `openssl rand -base64 32`.
2. In `.env`, keep the current key as a retired entry and set the new one with a higher version. Add the retired key to
   the API environment in `docker-compose.yml` (or an override file):

   ```yaml
       Storeasy__Encryption__RetiredKeys__1: ${STOREASY_RETIRED_KEY_1}
   ```

   ```dotenv
   STOREASY_MASTER_KEY=<new key>
   STOREASY_MASTER_KEY_VERSION=2
   STOREASY_RETIRED_KEY_1=<old key>
   ```

3. `docker compose up -d storeasy-api`. New secrets are sealed with version 2; records sealed with version 1 still open.
4. Re-seal old records: there is no bulk re-seal command yet. Add each Apple and Google connection again (delete and
   add; a Microsoft connection can instead get its client secret replaced), save the demo passwords again, and rotate
   the secrets of webhook endpoints, webhook subscriptions and pull sources (then give the new secrets to the senders).
   Queued e-mails are sent and removed on their own. Check what is left:

   ```bash
   docker compose exec postgres psql -U storeasy -d storeasy \
     -c 'SELECT "KeyVersion", count(*) FROM "StoreConnections" GROUP BY 1;' \
     -c 'SELECT "KeyVersion", count(*) FROM "SavedAnswers" WHERE "KeyVersion" IS NOT NULL GROUP BY 1;' \
     -c 'SELECT "KeyVersion", count(*) FROM "WebhookEndpoints" GROUP BY 1;' \
     -c 'SELECT "KeyVersion", count(*) FROM "OrganizationWebhooks" GROUP BY 1;' \
     -c 'SELECT "KeyVersion", count(*) FROM "ArtifactPullSources" GROUP BY 1;' \
     -c 'SELECT "KeyVersion", count(*) FROM "EmailOutbox" GROUP BY 1;'
   ```

5. When no record uses the old version, remove the retired key.

## 8. Upgrades and migrations

1. Read the release notes.
2. Back up the database ([section 9](#9-backups)). Migrations have no down scripts: going back means restoring.
3. Update and restart:

   ```bash
   git pull
   export STOREASY_COMMIT=$(git rev-parse --short HEAD)
   docker compose build
   docker compose up -d
   ```

4. The API applies pending migrations at startup, before the job runner starts. A migration error is logged and the
   API keeps running on the old schema, so check:

   ```bash
   docker compose logs storeasy-api | grep -i "applying migrations"
   curl -fsS http://127.0.0.1:8080/api/v1.0/healthcheck/ready
   ```

   The admin **System** page then shows the new version and commit.

Run one API instance while a release with schema changes starts; scale out afterwards. New configuration keys come with
a default that keeps the previous behaviour; compare your `.env` with the new `.env.example` to see the optional ones.

One exception: `STOREASY_MODE` (single team or multi tenant) defaults to `SingleTeam`. An instance that serves several
customers sets `STOREASY_MODE=MultiTenant` before it upgrades; a server one team uses, initialized earlier with
`developer/database/init`, needs nothing (see the end of [section 5.1](#51-single-team-default)).

## 9. Backups

| What | How | Why |
|---|---|---|
| PostgreSQL | Daily `pg_dump`, plus one before every upgrade | All product data, sealed secrets, audit log, job history |
| SeaweedFS volume | Daily copy of the `seaweedfs-data` volume, or a sync of the bucket to other S3 storage | Uploaded screenshots, previews, mockup exports, packages (packages are temporary) |
| `.env` and the master key | Password manager or vault, separate from the dumps | Without the master key the sealed secrets cannot be opened |
| Redis | Not needed | Cache and short lived state only |

```bash
# Database dump (custom format)
docker compose exec -T postgres pg_dump -U storeasy -Fc storeasy > storeasy-$(date -u +%Y%m%dT%H%M%SZ).dump

# Restore into the running stack (stop the API first)
docker compose stop storeasy-api
docker compose exec -T postgres pg_restore --clean --if-exists -U storeasy -d storeasy < storeasy-<stamp>.dump
docker compose start storeasy-api

# Object storage volume (stop SeaweedFS for a consistent copy)
docker compose stop seaweedfs
docker run --rm -v storeasy_seaweedfs-data:/data:ro -v "$PWD":/backup alpine tar czf /backup/seaweedfs-$(date -u +%Y%m%d).tgz -C /data .
docker compose start seaweedfs
```

Copy the files off the server, keep daily backups for 14 days and a monthly one for 6 months, and restore one into an
empty stack now and then to prove it works.

## 10. Health, status and logs

| Endpoint | Meaning |
|---|---|
| `GET /api/v1.0/healthcheck` | Liveness: the process answers. Use this for restarts. |
| `GET /api/v1.0/healthcheck/ready` | Readiness: 200 when PostgreSQL and Redis answer, 503 otherwise, with the state of each check. Do not restart on 503 (a Redis outage is not fixed by restarting the API). |
| `GET /health/live`, `GET /health/ready` | The same two probes at the API's root, for orchestrators that reach the API container directly (port 8080). The web container proxies them too. |
| `GET /api/v1.0/system/status` | For the instance admin (admin area, **System**): version, commit, environment, PostgreSQL, Redis, object storage and mail provider (configured, healthy, latency), the SignalR backplane, whether the master key, Google sign in and web push are configured (yes or no only), job queues across organizations and e-mails waiting in the outbox. |

The API image is chiseled (no shell), so it has no Docker `HEALTHCHECK`; watch it from your proxy or monitoring.
Bearer tokens, `access_token` query values, signed URL queries and store keys are redacted from the logs.

## 11. Retention and scheduled work

Every API instance runs these, in UTC. The syncs and reminders follow `Storeasy:Events:SchedulesEnabled`; the rule
watcher also needs `Storeasy:RuleWatcher:Enabled`.

| Work | When | What |
|---|---|---|
| Version state sync | Every 15 minutes | App Store version states; a rejection opens a rejection case. |
| winget sync | Every 15 minutes (minute 10) | Review state of open winget pull requests (labels, checks, comments); a merge reads the package again. |
| Chrome Web Store sync | Every 15 minutes (minute 12) | Status of Chrome Web Store items with a submission in review or an upload in progress. |
| Review sync | Every 30 minutes | New App Store, Google Play and Chrome Web Store reviews. |
| Firefox Add-ons sync | Every 15 minutes (minute 13) | Add-ons with a version awaiting review: status and the review result of their versions. |
| Review sync | Every 30 minutes | New reviews of every store with reviews (App Store, Google Play, Firefox Add-ons). |
| TestFlight sync | Every 6 hours | Groups, testers, builds and their expiry. |
| Deadline reminders | Daily 08:00 | TestFlight build expiry (14, 7, 1 days), compliance deadlines (30, 7, 1 days), Microsoft client secret expiry (30, 7, 1 days). |
| Rule watcher | Daily 06:00 | Reads the official store pages and files catalog proposals for the instance admin. |
| Audit log cleanup | Daily 03:30 | Deletes entries older than the organization's retention (90, 180, 365 or 730 days; default 365). |
| Package cleanup | Hourly | Packages past the organization's retention (default 30 days), abandoned uploads, unconfirmed asset uploads, expired upload tokens. |
| Notification cleanup | Every 5 days | In-app notifications seen more than 30 days ago, unseen ones older than 60 days. |
| Activity log cleanup | Every 30 days | Platform activity logs older than 60 days. |
| Idempotency keys | Hourly | Retry keys of create requests past their window. |

## 12. License and network use (AGPL)

storeasy is licensed under AGPL-3.0-only (see [`LICENSE`](../LICENSE) and [`NOTICE`](../NOTICE)).

- Running an unmodified copy for your team needs nothing extra.
- If you modify storeasy and let people use your version over a network, section 13 of the license requires you to
  offer them the corresponding source code of your version. The simplest way: publish your fork and set
  `STOREASY_SOURCE_URL` (build argument `VITE_SOURCE_URL`) so the "Source code" link in the web app points at it.

## 13. Known limits

- Real store calls have not been exercised end to end yet: the store engines are tested against fake App Store
  Connect, Google Play and Microsoft Store servers. Only the Apple and Google connection roles in
  [connecting-stores.md](connecting-stores.md) were checked live.
- The App Store Connect build upload path (`STOREASY_APPLE_UPLOAD_MODE=Api`) still needs one real build to confirm it;
  `External` is the fallback.
- The API trusts exactly two forwarding hops. A CDN in front of your proxy adds a third hop; then client addresses in
  logs and per-address rate limits show the CDN.
- The scheduled work of [section 11](#11-retention-and-scheduled-work) runs on every instance that has it enabled;
  with several instances turn `STOREASY_SCHEDULES_ENABLED` off on all but one.
- Re-sealing records after a master key rotation is manual ([section 7](#7-master-key-backup-and-rotation)).
