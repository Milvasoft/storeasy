# storeasy client tools: HTTP contract

This is the server side contract that the client tools in `tools/` depend on: the `storeasy` CLI, the `storeasy-mcp`
stdio bridge, the GitHub Action (`tools/upload-action`) and the Azure DevOps task `StoreasyUpload@1`
(`tools/azure-devops-task`). `storeasy-api` implements exactly this. The tests in `tools/storeasy-cli/test/fakeServer.ts`
are a runnable model of it: when in doubt, the fake server shows the expected request and response.

Sources: `docs/urun-plani.md` sections 2.6, 11.7 and 11.8; design files `13-Paket-Alma-ve-CI.html` (13.x),
`16-Hesap-ve-MCP.html` (16.x) and `20-Backend-Uyumu.html` (endpoint names). Where this file adds something the design
does not name, the row says **New (contract)**.

## 1. Conventions

| Topic | Rule |
|---|---|
| Base URL | `STOREASY_URL` or the saved config, default `https://storeasy.milvasoft.com`. REST under `/api/v1.0`, remote MCP at `/mcp`, same origin. |
| JSON | Responses are camelCase (ASP.NET default). Requests are sent camelCase; model binding is case insensitive. |
| Enums | Numeric, as in the rest of the API. The values for the new enums are fixed in section 7. The clients also accept the enum name as a string, so a later `JsonStringEnumConverter` does not break them. |
| Envelope | Every REST response is `Response<T>`: `{ isSuccess, statusCode, messages: [{ key, message, type }], data }`. Lists add `currentPageNumber`, `totalPageCount`, `totalDataCount`. The HTTP status matches `statusCode`. |
| Errors | `isSuccess: false` with at least one message. Clients show `messages[0].message` and branch on `messages[].key` and the HTTP status only. Message text never contains a token, a presigned URL query or a claim value. |
| Auth header | `Authorization: Bearer <token>` on every REST call and on `/mcp`. |
| Client header | `User-Agent: storeasy-cli/<version> (<os>; node <version>)`, `storeasy-mcp/<version>`, `storeasy-upload-action/<version>` or `storeasy-azure-task/<version>`. The server may record it as the client name (16.15 "Istemci"). |
| Organization | PAT, CI token and upload token are each bound to one organization. On requests made with them the server takes `organizationId` from the token and ignores any `organizationId` in the body or query. The clients never send it. |
| Retries | The clients retry on network errors, `429` (honoring `Retry-After` in seconds) and `5xx`: GET calls, session open (deduplicated by SHA-256) and `complete`. These calls must therefore be idempotent as described below. |
| Rate limits | `429` with `Retry-After` and message key `TooManyRequests`. |

## 2. Tokens and identity

| Kind | Prefix | Issued by | Bound to | Notes |
|---|---|---|---|---|
| Personal access token (PAT) | `sea_` | `POST /api/v1.0/tokens/token` (16.12) | user + organization, optional read only | Effective permission per request is `min(token, membership role)`. |
| CI token | `sci_` | `POST /api/v1.0/artifacts/ci-token` (13.9) | organization, app ids or all apps, scope `artifacts:write` | Not tied to a person. Expired: `401 CiTokenExpired`. Revoked: `401 TokenRevoked`. |
| Upload token | `sut_` **New (contract)** | `POST /api/v1.0/oidc/exchange` | one app, scope `artifacts:write`, 15 minutes | Opaque to clients. The prefix lets the log redaction recognize it. |

Only SHA-256 hashes of token values are stored. No endpoint ever returns a token value except the creation responses.
Clients treat every token as a secret: they never print it, and redact the prefixes above from all output.

Auth failures, for every endpoint:

| Status | Key | Meaning |
|---|---|---|
| 401 | `Unauthorized` | Missing, malformed or unknown token. |
| 401 | `TokenExpired` / `CiTokenExpired` / `TokenRevoked` | As named. |
| 403 | `OrganizationForbidden` | The token or role lacks the permission (for example a read only PAT on a write). |
| 403 | `AppForbidden` | A CI or upload token used for an app outside its scope. |

The clients map 401 and 403 to exit code 3.

### 2.1 `GET /api/v1.0/tokens/current` **New (contract)**

Describes the token of the request. Used by `storeasy whoami`, by `storeasy login` to validate a token before saving
it, and by the MCP bridge at start. Works for all three token kinds. Never returns the token value.

Response `data`:

```json
{
  "kind": 1,
  "name": "Claude Code laptop",
  "prefix": "sea_9c1",
  "organizationId": 7,
  "organizationName": "Kuzey Lab",
  "userName": "deniz@kuzeylab.dev",
  "readOnly": false,
  "scopes": [],
  "appIds": [],
  "allApps": true,
  "expiryDate": "2027-02-16T00:00:00Z"
}
```

`kind`: `TokenKind` (section 7). For a CI token `userName` is null, `scopes` is `["artifacts:write"]` and `appIds` /
`allApps` come from the token. For an upload token `appIds` has the one app and `expiryDate` is the 15 minute expiry.

## 3. Build intake (plan 11.8, design 13)

### 3.1 `POST /api/v1.0/oidc/exchange` (13.4)

Anonymous (the OIDC token is the credential). Exchanges a CI provider's OIDC token for a 15 minute upload token,
after matching it against the app's trusted publisher rules.

Request:

```json
{ "appId": 3, "token": "<OIDC JWT>", "provider": 1 }
```

`provider` (`OidcProvider`, section 7) is a hint from the client's CI detection; the server decides from the token's
`iss` and may ignore it.

Response `data`:

```json
{ "uploadToken": "sut_...", "expiryDate": "2026-11-18T14:13:00Z", "appId": 3 }
```

Rules the server applies:

- Verifies the signature against the issuer's JWKS, `exp`, `nbf`, `iat` (5 minute skew).
- Issuers: GitHub `https://token.actions.githubusercontent.com`; Azure DevOps `https://vstoken.dev.azure.com/<org id>`;
  GitLab `https://gitlab.com` or the self-managed URL stored on the rule.
- Audience: GitHub and GitLab tokens must carry `aud` = `storeasy` (the clients request exactly that). Azure DevOps
  issues workload identity tokens with the fixed audience `api://AzureADTokenExchange`; the server accepts that
  audience for `AzureDevOps` rules only (13.6).
- Claims compared with the rule: GitHub `repository`, `ref` (glob on branch, checked separately from `sub` because an
  environment job has no branch in `sub`), `environment`, optional `job_workflow_ref`; Azure DevOps `sub` =
  `sc://<org>/<project>/<service connection>`; GitLab `project_path`, `ref`, `ref_type`, `environment`.
- The raw token is never stored or logged; only the claims of the last exchange are kept for the diagnosis drawer
  (13.7).

Errors:

| Status | Key | When |
|---|---|---|
| 400 | `ValidationError` | Missing `appId` or `token`. |
| 401 | `OidcTokenInvalid` | Signature, expiry, unknown issuer or audience. |
| 403 | `OidcClaimMismatch` | Token is valid but no rule of this app matches. The message does not say which claim failed (13.7). |
| 404 | `AppNotFound` | Unknown app (same response whether it does not exist or has no rules, to avoid probing). |

### 3.2 `POST /api/v1.0/apps/{appId}/artifacts` (13.3, 13.22)

Opens an upload session, or resumes the open session for the same file, or reports a duplicate. Permission: PAT with
`ReleaseTest`, a CI token or upload token scoped to the app.

Request:

```json
{
  "fileName": "halka-1.0.0-42.aab",
  "size": 50331648,
  "sha256": "9e41c0d2...7b03",
  "contentType": "application/octet-stream",
  "source": 1,
  "sourceRef": {
    "provider": "github",
    "runId": "318",
    "runAttempt": "1",
    "runUrl": "https://github.com/kuzeylab/halka-mobile/actions/runs/318",
    "repository": "kuzeylab/halka-mobile",
    "branch": "main",
    "commit": "a41f9c2...",
    "actor": "deniz"
  },
  "delivery": { "store": 2, "track": "internal", "group": null, "notes": "Fixes the login crash" }
}
```

- `sha256`: lowercase hex of the whole file. `size` in bytes.
- `source`: `ArtifactSource`. The CLI sends `Cli` on a developer machine and the detected CI otherwise; the action sends
  `GitHubActions`, the Azure task `AzureDevOps`. `sourceRef` is optional and every field in it is optional.
- `delivery` is optional. Without it the app's delivery rule applies (13.18). With it, it overrides the target for this
  session only: `store` (`StoreKind`) must match the package's platform; `track` is a Play track name (`internal`,
  `alpha`, `beta` or a closed track); `group` a TestFlight internal group name; `notes` the release notes or "What to
  Test" text (max 4000 chars). Production tracks, external TestFlight groups and review submission are never accepted:
  `400 DeliveryTargetNotAllowed`.

Response `data` (`ArtifactUploadDto`), three shapes:

New session:

```json
{
  "sessionId": 4127,
  "duplicate": false,
  "resumed": false,
  "partSize": 8388608,
  "parts": [{ "partNumber": 1, "url": "https://s3.../...?X-Amz-Signature=..." }],
  "uploadedParts": [],
  "urlExpiryDate": "2026-11-18T15:58:00Z"
}
```

Resume. A session of the same app with the same `sha256` in status `Uploading` exists (opened by any token of the
organization within the retention window): the server lists the parts already in S3 and answers with the same
`sessionId`, `resumed: true`, `uploadedParts` (from S3 ListParts) and fresh presigned URLs for the missing parts
only. This is also how a client refreshes expired URLs: it posts the same body again.

```json
{
  "sessionId": 4127,
  "duplicate": false,
  "resumed": true,
  "partSize": 8388608,
  "parts": [{ "partNumber": 4, "url": "https://..." }],
  "uploadedParts": [{ "partNumber": 1, "eTag": "\"a1...\"" }, { "partNumber": 2, "eTag": "\"b2...\"" }],
  "urlExpiryDate": "2026-11-18T16:20:00Z"
}
```

Duplicate (13.17). A session of the same app with the same `sha256` exists that is past `Uploading` (any status except
a `Rejected` session whose package was deleted): no URLs, a new session row in status `Duplicate` is recorded for the
history, and the original is named.

```json
{ "sessionId": 4131, "duplicate": true, "originalSessionId": 4127, "parts": [], "uploadedParts": [] }
```

Part rules: `partSize` is constant for a session, between 5 MiB and 64 MiB; every part except the last has exactly
`partSize` bytes; part `n` covers bytes `[(n-1)*partSize, min(n*partSize, size))`; the number of parts is
`ceil(size / partSize)` (a 0 byte file is rejected with `400 ValidationError`). Presigned URLs accept a plain `PUT` of
the part's bytes with `Content-Length`, and S3 returns the part's `ETag` header (the bucket CORS must expose `ETag` for
the web app). URLs are valid at least 60 minutes.

Errors: `400 ValidationError` (size over the organization limit, default 4 GiB, unsupported extension: `.ipa`, `.aab`,
`.apk`, `.msix`, `.msixbundle`, `.appx`, `.appxbundle`, `.msixupload`, `.appxupload`, `.zip` for browser extensions), `400 DeliveryTargetNotAllowed`,
`404 AppNotFound`, `409 DeliveryStoreMismatch` (`delivery.store` is a store the app has no record in), `503
StorageUnavailable` (no bucket).

### 3.3 `PUT /api/v1.0/artifacts/{sessionId}/complete` (13.3)

Closes the multipart upload and starts processing (parse, check, rule, deliver).

Request:

```json
{ "parts": [{ "partNumber": 1, "eTag": "\"a1...\"" }, { "partNumber": 2, "eTag": "\"b2...\"" }] }
```

All parts, ascending. Response `data`: the session summary (`ArtifactSessionListDto`, 3.4) with status `Parsing` (or
later).

Idempotent: calling it again on a session that is already past `Uploading` returns the current summary with `200`.
Errors: `400 UploadIncomplete` (a part is missing or an ETag does not match S3; the message names the part numbers),
`400 SizeMismatch` (stored size differs from the declared size; the session is closed as `Rejected`), `404
ArtifactSessionNotFound`.

### 3.4 `GET /api/v1.0/artifacts/session?SessionId=` (13.14)

`ArtifactSessionDetailDto`, used by `--wait` (polled every 3 seconds by default; the server may answer `429` with
`Retry-After` and the clients slow down).

```json
{
  "id": 4127,
  "appId": 3,
  "appName": "Halka",
  "platform": 2,
  "fileName": "halka-1.0.0-42.aab",
  "size": 50331648,
  "sha256": "9e41c0d2...",
  "source": 1,
  "sourceRef": { "provider": "github", "runId": "318", "runUrl": "https://...", "repository": "kuzeylab/halka-mobile", "branch": "main", "actor": "deniz" },
  "status": 4,
  "statusDescription": "Delivering to Google Play internal",
  "startDate": "2026-11-18T13:58:02Z",
  "endDate": null,
  "storeJobId": 9182,
  "originalSessionId": null,
  "steps": [
    { "kind": 1, "status": 3, "message": "6 parts, 1 resumed", "bytes": 50331648, "total": 50331648, "resumeCount": 1, "startDate": "...", "endDate": "..." },
    { "kind": 2, "status": 3, "message": "com.kuzeylab.halka 1.0.0 (42)", "startDate": "...", "endDate": "..." },
    { "kind": 3, "status": 3, "message": "3 / 3 passed" },
    { "kind": 4, "status": 3, "message": "Android -> internal track" },
    { "kind": 5, "status": 2, "message": "edits.bundles.upload", "bytes": 32925696, "total": 50331648 },
    { "kind": 6, "status": 1 }
  ],
  "package": { "kind": 2, "identifier": "com.kuzeylab.halka", "versionName": "1.0.0", "versionCode": "42", "minOs": "26", "targetSdk": "36", "signature": "upload key SHA-256 3A:9F:...:E2" },
  "checks": [],
  "appliedRule": { "platform": 2, "target": 2, "targetId": "internal", "targetName": "Internal track" },
  "identity": { "kind": 1, "trustedPublisherId": 12, "ciTokenPrefix": null, "userName": null },
  "ciResult": null
}
```

`ciResult` is null until the session is final, then `{ "exitCode": 0, "summary": "Delivered Halka 1.0.0 (42) to
Google Play internal" }`. A session is final when `status` is `Delivered`, `Rejected`, `Duplicate`, `Held` or `Failed`
and `ciResult` is set. Exit codes (13.15, 13.21): `0` delivered or duplicate, `2` package rejected, `3` identity, `4`
store error, `5` (client side only) wait timed out. `Held` (a warning held the delivery for a human, 13.18 "OnWarning:
Hold") has `exitCode` 0 and a summary that says so; production and review submission are never part of this flow.

For a `Duplicate` session `originalSessionId` is set; the clients then wait for the original session (13.17).

Errors: `404 ArtifactSessionNotFound` (also for a session of another organization or, for CI and upload tokens,
another app).

### 3.5 Not used by the tools

The remaining intake endpoints of 13.x (`artifacts/sessions`, `artifacts/summary`, `artifacts/session/deliver`,
`DELETE artifacts/session`, trusted publishers, CI tokens, delivery rules, sources, `/webhooks/eas/{sourceId}`,
`/webhooks/artifacts/{sourceId}`) are for the web app and the pull sources; the tools do not call them.

## 4. Assets (MCP bridge local files)

The bridge uploads local files with the existing asset endpoints (`AssetsController`, phase 1) and hands the asset ids
to the remote tool.

### 4.1 `POST /api/v1.0/assets/upload` (exists)

Request: `{ "purpose": 1, "fileName": "01-home.png", "contentType": "image/png", "size": 482113, "sha256": "..." }`.
Response `data` (`AssetUploadDto`): `{ "assetId": 551, "uploadUrl": "https://...", "partSize": null, "parts": [],
"expiryDate": "..." }`. Either `uploadUrl` (single `PUT`) or `partSize` + `parts` (one `PUT` per part, same part
rules as 3.2) is set.

Requirement for the tools: these endpoints must accept a PAT (organization from the token).

### 4.2 `POST /api/v1.0/assets/asset/complete` (exists)

Request: `{ "assetId": 551, "parts": [{ "partNumber": 1, "eTag": "..." }] }` (`parts` empty for a single `PUT`).
Response `data`: `AssetDto` with `id`. Retry safe.

## 5. Other REST endpoints the CLI calls

| Command | Endpoint | Status |
|---|---|---|
| `storeasy whoami` | `GET /api/v1.0/tokens/current` | **New (contract)**, 2.1 |
| `storeasy apps list` | `PATCH /api/v1.0/apps` with `{ "pageNumber": 1, "rowCount": 100 }`, paged until `currentPageNumber >= totalPageCount` | Exists (`AppsController`), must accept a PAT |
| `storeasy jobs get <id>` | `GET /api/v1.0/storeJobs/job?JobId=` → `StoreJobDetailDto` | Exists (`StoreJobsController`; design 20 calls it `jobs/job`, same endpoint), must accept a PAT |
| `storeasy upload` | 3.1 to 3.4 | |

`apps list` prints `id`, `name` and, per `stores[]`, `store` + `bundleId`.

## 6. Remote MCP (`/mcp`, plan 2.6 and 11.7, design 16.15)

Streamable HTTP (MCP spec 2025-06-18 or later), `Authorization: Bearer <PAT>`. Stateless or stateful both work for
the bridge; if the server issues `Mcp-Session-Id`, the bridge sends it back. `401` on a revoked token.

### 6.1 Local path arguments

A remote tool that takes files declares each file argument as asset ids and marks it with the JSON Schema extension
keyword `x-storeasy-upload` on that top level property of `inputSchema`:

```json
{
  "name": "screenshots_upload_dir",
  "inputSchema": {
    "type": "object",
    "properties": {
      "appId": { "type": "integer" },
      "locale": { "type": "string" },
      "displayType": { "type": "string" },
      "screenshots": {
        "type": "array",
        "items": { "type": "integer" },
        "description": "Screenshots in display order.",
        "x-storeasy-upload": { "kind": "folder", "purpose": 1, "accept": [".png", ".jpg", ".jpeg"] }
      }
    },
    "required": ["appId", "locale", "displayType", "screenshots"]
  }
}
```

| `x-storeasy-upload` field | Values |
|---|---|
| `kind` | `file`: the property is one asset id (`integer`). `files`: array of asset ids, one per given path. `folder`: array of asset ids, one per matching file directly inside the folder (not recursive), in natural file name order (`2.png` before `10.png`). |
| `purpose` | `AssetPurpose` (1 Screenshot, 2 Graphic, 3 MockupOutput, 4 BuildArtifact). |
| `accept` | Optional lower case extensions. For `folder` it filters the files; for `file` and `files` a path with another extension is refused before upload. |
| `maxFiles` | Optional, for `folder` and `files`. |

The bridge advertises such a property to local clients as a path (`string` for `file` and `folder`, array of `string`
for `files`) and drops the keyword; on a call it uploads the files (4.1, 4.2) and sends the asset ids in place of the
paths. A number (or array of numbers) given by the client is passed through as asset ids unchanged. Without the
bridge, a remote client sends asset ids it already has. The remote server must therefore validate these arguments as
asset ids of the token's organization (`404 AssetNotFound` as a tool error otherwise).

### 6.2 Bridge-only tools

The bridge adds `artifact_upload` (design 16.15 "Yerel dosya") locally: `{ path, appId, store?, track?, group?, notes?,
wait? }`, implemented with section 3 and the bridge's PAT. The remote server must not define a tool with this name.

## 7. Enum values

| Enum | Values |
|---|---|
| `TokenKind` | 1 PersonalAccessToken, 2 CiToken, 3 UploadToken |
| `OidcProvider` | 1 GitHub, 2 AzureDevOps, 3 GitLab |
| `ArtifactSource` | 1 GitHubActions, 2 AzureDevOps, 3 GitLab, 4 Cli, 5 Rest, 6 Eas, 7 Webhook, 8 Manual, 9 Sync |
| `ArtifactSessionStatus` | 1 Uploading, 2 Parsing, 3 Checking, 4 Delivering, 5 Delivered, 6 Rejected, 7 Duplicate, 8 Held, 9 Failed |
| `ArtifactStepKind` | 1 Upload, 2 Parse, 3 Check, 4 Rule, 5 Deliver, 6 Report |
| `ArtifactStepStatus` | 1 Pending, 2 Running, 3 Done, 4 Failed, 5 Skipped |
| `ArtifactPlatform` | 1 Ios, 2 Android, 3 Windows |
| `PackageKind` | 1 Ipa, 2 Aab, 3 Msix, 4 Apk |
| `DeliveryTarget` | 1 TestFlightGroup, 2 PlayTrack, 3 MsFlight, 4 None |
| `IdentityKind` | 1 Oidc, 2 CiToken, 3 User |
| `StoreKind` (exists) | 1 Apple, 2 Google, 3 Microsoft |
| `StoreJobStatus` (exists) | 1 Queued, 2 Running, 3 Succeeded, 4 Failed, 5 Cancelled |

The CLI's `--store` takes `apple`, `google` or `microsoft` and sends the `StoreKind` number.

## 8. What the clients guarantee

- Token values (PAT, CI token, OIDC token, upload token, the GitHub `ACTIONS_ID_TOKEN_REQUEST_TOKEN`, the Azure
  `System.AccessToken`) never appear in stdout, stderr, files other than the 0600 config file, or MCP tool results. In
  GitHub Actions and Azure Pipelines the OIDC and upload tokens are also registered with the runner's masking
  (`::add-mask::`, `##vso[task.setsecret]`).
- Presigned URLs are printed, if ever, without their query string.
- The SHA-256 sent is the hash of exactly the bytes uploaded.
- Parts are retried individually (4 attempts with backoff); a fresh process resumes an interrupted upload through 3.2.
  The CLI also keeps the ETags it has seen in `<config dir>/uploads/<sha256>.json` and deletes the file after
  `complete`.
