# Self-hosting storeasy

This guide runs storeasy on one Linux server with Docker: the API, the web app, PostgreSQL 17, Redis 7 and SeaweedFS
(S3 compatible object storage). Everything is in [`deploy/docker-compose.yml`](../deploy/docker-compose.yml) and
[`deploy/.env.example`](../deploy/.env.example).

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
10. [Health and logs](#10-health-and-logs)
11. [License and network use (AGPL)](#11-license-and-network-use-agpl)
12. [Known limits](#12-known-limits)

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
                    |
          postgres:5432   redis:6379
```

- **One origin.** The web container serves the app and proxies `/api`, `/hubs` and `/mcp` to the API, so there is no
  CORS setup and one certificate for the app.
- **Files never pass through the API.** Uploads and downloads use presigned URLs on the S3 endpoint, so the S3 API needs
  its own public host name (for example `files.storeasy.example.com`).
- **Store jobs run inside the API** (`StoreJobRunner`, a hosted service). No separate worker container.
- **Redis** holds the cache, token revocation stamps, feature flags, rate limit counters and the SignalR backplane.
  Losing it loses nothing permanent.

## 2. Requirements

- Linux server with Docker Engine 24+ and the Compose plugin. 2 vCPU and 4 GB RAM are enough for a team.
- Two DNS names pointing at the server: the app (`storeasy.example.com`) and the S3 endpoint
  (`files.storeasy.example.com`).
- An SMTP account for verification, password reset and invitation e-mails.
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

PowerShell equivalents: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` and
`[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`.

Store `STOREASY_MASTER_KEY` in a password manager now, outside the server (see
[section 7](#7-master-key-backup-and-rotation)).

Then:

```bash
docker compose up -d --build
docker compose ps
```

Startup order: PostgreSQL and Redis become healthy, `seaweedfs-config` writes the S3 identity from `.env` into a
volume, SeaweedFS starts, `seaweedfs-init` creates the bucket and exits, then the API starts and applies the database
migrations, then the web container.

### Images and the rules catalog

The API image ships the store rules catalog (`rules/` at the repository root): the build context of the API image is the
repository root, and the catalog is copied next to the application and embedded in it. The API validates the catalog at
startup and stops if it is invalid. To use another catalog without rebuilding, mount a folder and point
`Storeasy__Stores__RulesPath` at it (the commented lines in the compose file):

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
  inner nginx already allows one hour). Set `X-Forwarded-For` to the client address. The API trusts the last two
  `X-Forwarded-For` hops (your proxy and the inner nginx), so run exactly one proxy in front of the web container and
  never expose the web or API container directly.
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

The API image runs with `MILVA_ENV=prod`, which closes the developer endpoints, including database initialization.
Initialize a new database once with a temporary development instance of the same image:

```bash
docker compose stop storeasy-api
docker compose run --rm -e MILVA_ENV=dev -p 127.0.0.1:5080:8080 storeasy-api
```

1. Open `http://127.0.0.1:5080/api/documentation` on the server (use an SSH tunnel from your machine).
2. Call `POST /api/v1.0/developer/database/init`. It seeds the root user `rootuser` (the instance admin), roles,
   permissions and languages, and returns the generated root password once. Store it in a password manager. `init`
   refuses a database that is already initialized.
3. Stop the temporary container (Ctrl+C) and start the normal one: `docker compose up -d storeasy-api`.

Then create your own account:

1. With `STOREASY_REGISTRATION_MODE=Open`, register in the web app and verify your e-mail (SMTP must work).
2. Create your organization; you are its Owner.
3. Close registration: set `STOREASY_REGISTRATION_MODE=InviteOnly` (people join through invitations you send) or
   `Closed`, then `docker compose up -d storeasy-api`.

The root user is for instance administration (user locking, feature flags, system state), not for daily store work.

## 6. Configuration reference

The API reads `appsettings.json` and then environment variables; a variable name is the key with `:` replaced by `__`
(`Storeasy:Encryption:MasterKey` becomes `Storeasy__Encryption__MasterKey`). Values marked **secret** must only come
from the environment (or user secrets on a developer machine), never from a file in the repository.

### Core

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnectionString` | empty | **Secret.** PostgreSQL 17 connection string. |
| `Milvasoft:Caching:Redis:ConnectionString` | `redis` | Redis 7 as `host:port`. The API starts while Redis is down (readiness answers 503, rate limits fail open). |
| `CacheKeys:Prefix` | `storeasy` | Prefix of every Redis key and SignalR channel. Use a different one per instance when several share one Redis. |
| `Milvasoft:Identity:Token:SymmetricPublicKey` | empty | **Secret.** HMAC-SHA256 key for access tokens, at least 32 characters. Missing or short: the API stops at startup outside Development. |
| `Milvasoft:Identity:Token:ExpirationMinute` | `30` | Access token lifetime. |
| `Milvasoft:Identity:Token:TokenValidationParameters:ValidIssuer`, `ValidAudience` | `Storeasy` | Issuer and audience written into and required on tokens. Change both together. |
| `Milvasoft:Identity:Password:*` | 6 characters, upper, lower, digit, symbol | Password policy. |
| `Milvasoft:Identity:Lockout:MaxFailedAccessAttempts`, `DefaultLockoutTimeSpan` | `5`, `5` | Sign-in lockout (minutes). |
| `Milvasoft:Interception:Log:LogDefaultParameters` | `false` | Logs every argument and result of `[Log]` methods. Keep `false` in production: they can contain personal data. |
| `Milvasoft:FileOperations:Json:EncryptionKey` | empty | **Secret.** Not used by storeasy today. |
| `MILVA_ENV` | `prod` in the image | `dev`, `test` or `prod`. `prod` closes the developer endpoints and the API documentation and selects the log sinks. |
| `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS` | `Production`, `http://+:8080` in the image | Leave as is. |

The remaining `Milvasoft:*` keys in `appsettings.json` are library settings that need no change.

### storeasy

| Key | Default | Description |
|---|---|---|
| `Storeasy:App:BaseUrl` | empty | Public address of the web app. E-mail links point at `{BaseUrl}/verify-email`, `/reset-password` and `/invitation`. Required outside Development. |
| `Storeasy:Registration:Mode` | `Open` | `Open`, `InviteOnly` (a valid invitation for the same e-mail is required) or `Closed`. |
| `Storeasy:Encryption:MasterKey` | empty | **Secret.** Base64 of 32 random bytes. Seals store credentials and saved demo passwords (AES-256-GCM envelope encryption). |
| `Storeasy:Encryption:KeyVersion` | `1` | Version of the current master key. Raise it when rotating. |
| `Storeasy:Encryption:RetiredKeys:<version>` | none | **Secret.** Older master keys by version, so records sealed before a rotation still open. |
| `Storeasy:Email:Provider` | `Smtp` | `Smtp` (self-host) or `Notifike` (Milvasoft hosting). `Log` writes e-mails to the log and is refused outside Development. |
| `Storeasy:Email:FromName` | `storeasy` | Sender display name. |
| `Storeasy:Email:Smtp:Host`, `Port`, `UserName`, `From`, `EnableSsl` | empty, `587`, empty, empty, `true` | SMTP server. `Host` and `From` are required with `Smtp`; otherwise the API stops at startup and names the missing key. |
| `Storeasy:Email:Smtp:Password` | empty | **Secret.** SMTP password. |
| `Storeasy:Email:Notifike:BaseUrl`, `ApiKey` | empty | Notifike API (`ApiKey` is **secret**). Both required with `Notifike`. |
| `Storeasy:Email:Outbox:Enabled` | `true` | Whether this instance runs the background e-mail sender. E-mails are never sent on the request path: they are queued in the `EmailOutbox` table (body sealed with the master key) and sent from there, so a slow provider or a missing account does not change answer times. |
| `Storeasy:Email:Outbox:PollIntervalMilliseconds`, `MaxAttempts`, `RetryBaseDelaySeconds`, `LeaseSeconds` | `1000`, `6`, `30`, `120` | Sender pause, attempts of one e-mail (then it is dropped and logged), doubling retry delay (at most an hour), claim lease. |
| `Storeasy:Auth:Google:ClientId`, `ClientSecret` | empty | Sign in with Google (`ClientSecret` is **secret**). Off while either is empty. See [Sign in with Google](#sign-in-with-google). |
| `Storeasy:Auth:Google:Authority`, `StateLifetimeMinutes` | `https://accounts.google.com`, `10` | OpenID Connect issuer (https only; change it only for testing) and how long a started sign in may take. |
| `Storeasy:WebPush:PublicKey`, `PrivateKey` | empty | VAPID key pair of browser notifications (`PrivateKey` is **secret**). Off while either is empty. See [Browser notifications](#browser-notifications-web-push). |
| `Storeasy:WebPush:Subject`, `TimeToLiveSeconds` | `mailto:admin@localhost`, `86400` | Contact the push services may use (`mailto:` or https) and how long they keep an undelivered push. |
| `Storeasy:Jobs:Enabled` | `true` | Whether this instance runs store jobs. Every instance can queue them. |
| `Storeasy:Jobs:Workers` | `2` | Jobs one instance runs at the same time. |
| `Storeasy:Jobs:PollIntervalMilliseconds` | `2000` | Pause of an idle worker. |
| `Storeasy:Jobs:LeaseSeconds` | `300` | Lease of a running attempt, renewed on every step; an expired lease lets another worker reclaim the job. |
| `Storeasy:Jobs:DefaultMaxAttempts` | `3` | Attempts of a job. |
| `Storeasy:Jobs:RetryBaseDelaySeconds`, `RetryMaxDelaySeconds` | `30`, `900` | Exponential backoff between attempts. |
| `Storeasy:Stores:MaxRetries` | `3` | Retries of a store call answered with 429 or 5xx. |
| `Storeasy:Stores:RetryBaseDelayMilliseconds`, `RetryMaxDelaySeconds` | `800`, `60` | Backoff of those retries; a `Retry-After` header wins. |
| `Storeasy:Stores:AssetPollIntervalMilliseconds`, `AssetPollTimeoutSeconds` | `2000`, `120` | Wait for Apple to process an uploaded screenshot. |
| `Storeasy:Stores:UploadChunkBytes` | `8388608` | Chunk size of resumable Play bundle uploads (a multiple of 256 KiB). |
| `Storeasy:Stores:PlanLifetimeMinutes` | `2880` | How long a pending change plan can be approved (48 hours). |
| `Storeasy:Stores:RulesPath` | empty | Folder with `catalog.json` and the store files. Empty: the copy next to the application, then the embedded copy. |
| `Storeasy:Intake:AppleUploadMode` | `Api` | `Api`: storeasy uploads IPAs with the App Store Connect build upload API. `External`: the CI uploads to Apple itself and storeasy waits for the build, then applies the delivery rule. |
| `Storeasy:Intake:UrlLifetimeMinutes` | `120` | Lifetime of the presigned part URLs of package uploads (at least 60). |
| `Storeasy:Intake:ProcessMaxAttempts` | `40` | Attempts of a package processing job (an iOS delivery waits for Apple's processing by retrying). |
| `Storeasy:Intake:AbandonedUploadHours` / `PendingAssetHours` | `48` | When unfinished package uploads and unconfirmed asset uploads are cleaned up. |

### Sign in with Google

1. In Google Cloud Console create an OAuth client of type **Web application** (APIs and Services, Credentials). The
   consent screen needs only the `openid`, `email` and `profile` scopes.
2. Add the authorized redirect URI `<STOREASY_BASE_URL>/auth/google/callback` (the web app's callback page; it sends
   the code and state to `POST /api/v1.0/account/login/google`).
3. Set `Storeasy__Auth__Google__ClientId` and `Storeasy__Auth__Google__ClientSecret` in `.env` and restart the API.

The flow is the authorization code flow with PKCE; the code verifier and nonce never leave the API (Redis, ten minutes,
single use), so Redis is required for it. An account is linked only by an address Google reports as verified; an
existing account that never verified its address is taken over by that proof (its password is replaced with a random
one and its sessions end). New accounts follow `Storeasy:Registration:Mode`: `InviteOnly` needs an invitation for the
same address, `Closed` creates none (existing accounts can still sign in). No Google call is made while the client id
or secret is empty.

### Browser notifications (web push)

Pushes go straight from the API to the browsers' push services with VAPID (RFC 8292) and the payload encrypted to the
browser (RFC 8291); no third party account or library is involved. Generate one P-256 key pair per instance:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out vapid.pem
# Public key: the 65 byte uncompressed point, base64url
openssl ec -in vapid.pem -pubout -outform DER 2>/dev/null | tail -c 65 | base64 | tr '/+' '_-' | tr -d '=\n'; echo
# Private key: the 32 byte scalar, base64url
openssl ec -in vapid.pem -outform DER 2>/dev/null | tail -c +8 | head -c 32 | base64 | tr '/+' '_-' | tr -d '=\n'; echo
```

Put them into `Storeasy__WebPush__PublicKey` and `Storeasy__WebPush__PrivateKey`, set `Storeasy__WebPush__Subject` to a
contact address and delete `vapid.pem`. The web app reads the public key from `GET /api/v1.0/system/public-settings`.
Changing the key pair invalidates every browser subscription (users subscribe again). The API sends pushes only to
public addresses (the same guard as outgoing webhooks), so the server needs outbound HTTPS to the push services
(`fcm.googleapis.com`, `updates.push.services.mozilla.com`, `*.notify.windows.com`, `web.push.apple.com`).

### Object storage

| Key | Default | Description |
|---|---|---|
| `Storage:S3:Bucket` | empty | Private bucket. Empty disables uploads (the endpoints answer 503). |
| `Storage:S3:Region` | `eu-central-1` | Bucket region; for SeaweedFS any value works as the signing region. |
| `Storage:S3:AccessKey`, `SecretKey` | empty | **Secret.** Bucket credentials. Without an access key the AWS default credential chain is used. |
| `Storage:S3:ServiceUrl` | empty | S3 compatible endpoint (SeaweedFS). Empty for AWS S3. Must be reachable by browsers, CI runners and the API. |
| `Storage:S3:ForcePathStyle` | `true` | Path style addressing (`host/bucket/key`), needed by SeaweedFS. Ignored without `ServiceUrl`. |
| `Storage:S3:PresignedUrlLifetimeMinutes` | `30` | Lifetime of presigned URLs. |

AWS S3 works too: leave `ServiceUrl` empty, set the region, block public access on the bucket and add the CORS rule from
[section 4](#4-reverse-proxy-and-tls). SeaweedFS can then be removed from the compose file.

### Observability

| Key | Default | Description |
|---|---|---|
| `SeqUri` | empty | Seq server (`MILVA_ENV` `test` or `prod`). Empty disables Seq. |
| `OpenTelemetry:CollectorUrl` | empty | OTLP gRPC endpoint for logs, metrics and traces (`prod`). Empty disables OpenTelemetry. |
| `OpenTelemetry:ServiceName` | `storeasy-api` | Service name in logs, metrics and traces. |

With both empty in `prod`, logs go to the console (`docker compose logs storeasy-api`).

### Web app

| Key | When | Description |
|---|---|---|
| `VITE_SOURCE_URL` | build | "Source code" link in the footer and account menu (AGPL section 13). Empty: the upstream repository. In `.env`: `STOREASY_SOURCE_URL`. |
| `VITE_APP_VERSION` | build | Version shown in the menu. In `.env`: `STOREASY_VERSION`. |
| `VITE_API_URL` | build | API origin. Keep empty for the single origin setup. |
| `STOREASY_API_UPSTREAM` | runtime | Where nginx proxies `/api`, `/hubs` and `/mcp` (default `http://storeasy-api:8080`). |

`VITE_*` values are baked into the image: changing one means `docker compose build storeasy-web`.

## 7. Master key: backup and rotation

The master key seals every store credential (App Store Connect keys, Play service accounts, later Microsoft Entra
secrets) and saved demo passwords. Each record gets its own data key; the master key wraps the data keys. A database
dump alone does not reveal any credential, and the key alone is useless without the database.

**Backup.** Keep `STOREASY_MASTER_KEY` and its version in a password manager or vault, separate from database backups.
If the key is lost, every store connection must be added again and saved demo passwords entered again.

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
4. Re-seal old records: there is no bulk re-seal command yet. Add each store connection again (delete and add) and save
   the demo passwords again. Check what is left:

   ```bash
   docker compose exec postgres psql -U storeasy -d storeasy \
     -c 'SELECT "KeyVersion", count(*) FROM "StoreConnections" GROUP BY 1;' \
     -c 'SELECT "KeyVersion", count(*) FROM "SavedAnswers" WHERE "KeyVersion" IS NOT NULL GROUP BY 1;'
   ```

5. When no record uses the old version, remove the retired key.

## 8. Upgrades and migrations

1. Read the release notes.
2. Back up the database ([section 9](#9-backups)). Migrations have no down scripts: going back means restoring.
3. Update and restart:

   ```bash
   git pull
   docker compose build
   docker compose up -d
   ```

4. The API applies pending migrations at startup, before the job runner starts. A migration error is logged and the
   API keeps running on the old schema, so check:

   ```bash
   docker compose logs storeasy-api | grep -i "applying migrations"
   curl -fsS http://127.0.0.1:8080/api/v1.0/healthcheck/ready
   ```

Run one API instance while a release with schema changes starts; scale out afterwards.

## 9. Backups

| What | How | Why |
|---|---|---|
| PostgreSQL | Daily `pg_dump`, plus one before every upgrade | All product data, sealed credentials, audit log |
| SeaweedFS volume | Daily copy of the `seaweedfs-data` volume, or a sync of the bucket to other S3 storage | Uploaded screenshots, mockups, packages (packages are temporary) |
| `.env` and the master key | Password manager or vault, separate from the dumps | Without the master key the sealed credentials cannot be opened |
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

## 10. Health and logs

| Endpoint (through the web container) | Meaning |
|---|---|
| `GET /api/v1.0/healthcheck` | Liveness: the process answers. Use this for restarts. |
| `GET /api/v1.0/healthcheck/ready` | Readiness: 200 when PostgreSQL and Redis answer, 503 otherwise. Do not restart on 503 (a Redis outage is not fixed by restarting the API). |

The API image is chiseled (no shell), so it has no Docker `HEALTHCHECK`; watch it from your proxy or monitoring.
Bearer tokens, `access_token` query values, signed URL queries and store keys are redacted from the logs.

## 11. License and network use (AGPL)

storeasy is licensed under AGPL-3.0-only (see [`LICENSE`](../LICENSE) and [`NOTICE`](../NOTICE)).

- Running an unmodified copy for your team needs nothing extra.
- If you modify storeasy and let people use your version over a network, section 13 of the license requires you to
  offer them the corresponding source code of your version. The simplest way: publish your fork and set
  `STOREASY_SOURCE_URL` (build argument `VITE_SOURCE_URL`) so the "Source code" link in the web app points at it.

## 12. Known limits

- The web `Dockerfile` builds for `linux/arm64` (the upstream hosting runs on ARM). On an x86_64 server, either enable
  arm64 emulation (`docker run --privileged --rm tonistiigi/binfmt --install arm64`) or build the web image without the
  platform pin.
- The API trusts exactly two forwarding hops. A CDN in front of your proxy adds a third hop; then client addresses in
  logs and per-address rate limits show the CDN.
- Remote MCP, personal access tokens, CI intake (OIDC, CI tokens, EAS and generic webhooks), App Store Connect
  webhooks, the studio and Microsoft Store are being built (plan phases 3 to 6). Their settings will be added here
  when they land.
