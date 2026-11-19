# REST API

A short map of the storeasy REST surface. The full reference, with every request and response shape, is the API's
OpenAPI document: `/api/docs/v1.0/docs.json`, browsable at `/api/documentation`. Both are served only when `MILVA_ENV`
is exactly `dev` (a local run); `test`, `prod` and any other value close them (404). The page carries no token: sign in
with `POST /api/v1.0/account/login` and paste the access token into the page. The calls the client tools rely on are pinned in [`tools/CONTRACT.md`](../tools/CONTRACT.md).

## Conventions

- Base path `/api/v1.0/`, lower case (`/api/v1.0/storejobs/job`). Through the web container every call goes to the app's
  own origin.
- Lists are `PATCH` with a filter, sort and paging body (`PATCH /api/v1.0/apps`); single records are read with query
  parameters (`GET /api/v1.0/apps/app?AppId=3`).
- Every answer is the same envelope: `isSuccess`, `statusCode`, `messages` (localized by `Accept-Language`) and
  `data`. Errors carry a stable message key (`EmailNotVerified`, `DeliveryTargetNotAllowed`, ...).
- Organization scoped requests carry `organizationId`; a personal access token, CI token or upload token replaces it
  with the token's own organization.
- Creating calls that queue store work accept an `Idempotency-Key` header, so a retried request replays the first
  answer.
- Anonymous endpoints and costly calls are rate limited (429).

## Authentication

| Credential | How it is sent | Accepted by |
|---|---|---|
| Web session (JWT access token, refresh token) | `Authorization: Bearer`; `?access_token=` for the SignalR hub | Everything except `/mcp` |
| Personal access token `sea_...` | `Authorization: Bearer` | The REST API and `/mcp`, for one organization; read only tokens are capped to Viewer |
| CI token `sci_...` | `Authorization: Bearer` | Package upload endpoints of its apps |
| Upload token `sut_...` (15 minutes, from `POST oidc/exchange`) | `Authorization: Bearer` | Package upload endpoints of its one app |
| Webhook signature | HMAC headers | The anonymous receivers under `/api/webhooks/...` |

Deny by default: every endpoint needs an authenticated caller unless it is marked anonymous.

## Permissions

Two layers:

- **Organization permissions** decide store and team work. The seeded roles are Owner, Admin, Editor and Viewer; an
  organization can also define custom roles from the same permissions (never more than the member who assigns them
  holds, never `ManageOrganization`). The check runs per request from the current membership, the same for the web
  app, REST and MCP.

  | Permission | Default roles |
  |---|---|
  | `View` | All |
  | `EditContent`, `ReleaseTest`, `ReplyReviews` | Editor, Admin, Owner |
  | `ReleaseProduction`, `SubmitForReview`, `ManageConnections`, `ManageWebhooks`, `ManageMembers` | Admin, Owner |
  | `ManageOrganization` | Owner |

- **Platform permissions** (`UserManagement.*`, `RoleManagement.*`, `FeatureFlagManagement.*`,
  `RuleCatalogManagement.*`, `SystemManagement.*`, ...) belong to the instance admin (`rootuser`) and platform roles.

Creating an organization or a store connection also needs a verified e-mail address.

## Areas

| Area | Routes (under `/api/v1.0/`) | Auth and permission |
|---|---|---|
| Account | `account/login`, `login/google/start`, `login/google`, `login/refresh`, `register`, `email/verify`, `password/forgot`, `password/reset` | Anonymous, rate limited |
| | `account/detail`, `profile`, `password/change`, `logout`, `sessions`, `session`, `sessions/others`, `push/subscription(s)`, `push/test`, `notifications`, `account/preferences` | Signed in user, own data only |
| Organizations and members | `organizations/mine`, `organization`, `settings`, `members`, `member/role`, `member`, `invitation`, `invitation/accept`, `roles`, `role`, `audit-logs` | `View`; members and invitations `ManageMembers`; custom roles `ManageMembers` within your own permissions; rename, delete and owners `ManageOrganization`; `invitation/preview` anonymous |
| | `organizations/ai-settings`, `ai-settings/key` (PUT, DELETE), `ai-settings/models` (PUT) | Read `View` (whether a key is set, its last four characters, the model of each AI feature and the models to choose from with their prices; never the key); save and remove the Anthropic API key and choose the models `ManageConnections` |
| Store connections | `storeconnections`, `connection`, `connection/verify` | List `View`; add, verify, delete `ManageConnections` |
| Apps and readiness | `apps`, `apps/app`, `apps/google`, `apps/app/link`, `apps/app/unlink`, `apps/app/hidden`, `apps/app/order`, `apps/sync`, `apps/profile`, `apps/readiness`, `apps/overview`, `apps/compliance`, `apps/answers*`, `compliance`, `rules/*` | Reads `View`; changes `EditContent` |
| Change plans | `plans`, `plans/plan`, `plans/approve`, `plans/reject`, `plans/discard` | Read `View`; approve with the plan's own permission and, for submissions and production, the typed confirmation |
| Store content | `listings`, `screenshots`, `previews`, `categories`, `age-rating`, `pricing`, `review-info`, `builds`, `releases` (each with reads, `plan` and `apply`) | Reads `View`; plans `EditContent`; test releases `ReleaseTest`; production and App Store rollouts `ReleaseProduction`; `releases/submit-for-review` `SubmitForReview` |
| Money | `subscriptions/*`, `iap/*`, `purchasesettings/*` | Reads `View`; plans `EditContent`; server notification URLs `ManageWebhooks` |
| TestFlight | `testflight/*` | Reads `View`; changes and beta review `ReleaseTest` |
| Microsoft Store | `ms/product`, `ms/submissions`, `ms/flights`, `ms/addons`, `ms/packages`, `ms/content`, `ms/change`, `ms/submission/submit`, `ms/packages/verify`, `ms/flight-group`, `ms/connection/secret`, `ms/connections/expiring` | Reads `View`; changes `EditContent`; submit `SubmitForReview` with the Store id typed; secret replacement `ManageConnections` |
| AI pre-review | `pre-reviews` (GET), `pre-reviews/pre-review` (GET, POST) | History and report `View`; start `EditContent`, 20 an hour per user, one at a time per app and store ([AI pre-review](#ai-pre-review)) |
| Assisted steps | `assisted-steps`, `assisted-steps/step`, `step/handoff`, `step/complete` | Read `View`; hand off and complete `EditContent` |
| Reviews and rejections | `reviews/*`, `rejections/*` | Reads `View`; replies `ReplyReviews`; rejection cases `EditContent` |
| | `reviews/reply-draft/generate` | `ReplyReviews`, 60 an hour per user: Claude writes a reply draft with the organization's Anthropic API key and it is saved as the review's draft; nothing is sent to the store ([AI reply drafts](#ai-reply-drafts)) |
| Studio | `mockups/*` (templates, captions, frames, previews, exports) | Reads and previews `View`; templates and exports `EditContent`; `captions/suggest` behind the `Studio.AiCaptions` flag |
| Files | `assets/upload`, `assets/asset/complete`, `assets/asset/download-url` | `EditContent` to upload, `View` to download; bytes go to S3 through presigned URLs |
| Store jobs | `storejobs`, `storejobs/job`, `storejobs/job/cancel` | Read `View`; cancel `EditContent`; live on the SignalR hub `/hubs/activity` |
| Package intake | `apps/{appId}/artifacts`, `artifacts/{sessionId}/complete`, `artifacts/session(s)`, `artifacts/summary`, `artifacts/counts`, `artifacts/session/deliver` | Upload with a PAT `ReleaseTest`, or a CI or upload token; deliver held `ReleaseTest`; reads `View` |
| | `artifacts/ci-token(s)`, `artifacts/trusted-publisher(s)`, `artifacts/delivery-rules`, `artifacts/source(s)`, `artifacts/settings` | Reads `View`; changes `ManageConnections` |
| | `oidc/exchange` | Anonymous; the CI provider's OIDC token is the credential |
| Personal access tokens | `tokens`, `tokens/token`, `tokens/current` | Created and revoked from a web session only; `tokens/current` describes the calling token |
| Webhooks | `webhooks`, `webhooks/endpoint`, `endpoint/rotate-secret`, `events`, `event/reprocess`, `subscription(s)`, `subscription/rotate-secret`, `subscription/test`, `deliveries` | `ManageWebhooks` |
| Webhook receivers | `/api/webhooks/apple/{id}`, `/api/webhooks/signed/{id}`, `/api/webhooks/eas/{id}`, `/api/webhooks/artifacts/{id}` (no version segment) | Anonymous, HMAC signature, rate limited, size limited |
| System | `system/public-settings` | Anonymous: deployment mode (`SingleTeam` or `MultiTenant`), whether the first run setup is required, registration mode, Google sign in on or off, web push public key |
| System | `system/setup` | Anonymous, single team only, rate limited: with the one time setup code from the API log, creates the administrator and the team and returns the login tokens; 403 wrong code, 409 once a user exists, 404 in multi tenant mode ([self-host.md](self-host.md#51-single-team-default)) |
| | `system/feature-flags` | Any signed in user (read only) |
| | `system/status`, `system/settings`, `system/settings/registration-mode` | `SystemManagement.Status`, `SystemManagement.Settings` |
| Platform admin | `users`, `roles`, `permissions`, `featureflags`, `languages`, `activitylogs`, `rules/sources`, `rules/proposals` | Platform permissions |
| Health | `healthcheck`, `healthcheck/ready` (also `/health/live`, `/health/ready` at the API root) | Anonymous |
| Developer | `developer/database/*`, `developer/export/*`, `developer/import/*` | Super admin; `developer/database/init` is anonymous with the bootstrap key header `X-Storeasy-Init-Key` ([self-host.md](self-host.md#5-first-admin-and-first-account)). Open only when `MILVA_ENV=dev` (404 otherwise) |

## AI reply drafts

"Write with AI" on the reviews page asks Claude for a reply draft. It is manual only: a member presses the button, reads
and edits the draft, and sends it with the usual reply flow; nothing is ever sent to a store without a person.

- **Key.** Each organization saves its own Anthropic API key in Settings, AI (`PUT organizations/ai-settings/key`
  `{OrganizationId, ApiKey}`, `ManageConnections`). It is checked with Anthropic first by reading the model (no cost): a
  key Anthropic rejects is refused (409 `AiKeyRejected`); when Anthropic cannot be reached it is saved with a warning
  (`ApiKeyVerified: false`). It is sealed with the envelope encryption of the store credentials, bound to the
  organization, and never returned, logged or written to the audit log; only its last four characters are shown.
  Audited as `AiKeySet` and `AiKeyRemoved`.
- **Draft.** `POST reviews/reply-draft/generate` `{OrganizationId, ReviewId}` returns
  `{ReviewId, Body, Shortened, Model}` and saves `Body` as the review's draft. The model gets a fixed system prompt
  (polite, specific to the review, no promises or invented facts, no personal data or links, the review's language) and
  the review as escaped data, with the store's limit. A reply longer than the limit (Google Play 350, App Store 5,970
  characters) is cut after its last full sentence that fits (`Shortened: true`) as long as that keeps at least half of
  the limit; otherwise the call fails. Audited as `ReviewReplyDraftGenerated` with the model and token counts only.
- **Errors.** No key 409 `AiKeyNotSet`; rejected key 409 `AiKeyRejected`; Anthropic rate limit 429 `AiRateLimited`;
  overloaded or down 503 `AiUnavailable`; the model declined 422 `AiReplyRefused`; anything else (an answer cut short,
  a reply that cannot be fitted) 502 `AiReplyFailed`. Anthropic's own error text is never passed on.
- **Model.** The organization's choice (`PUT organizations/ai-settings/models`), otherwise `Storeasy:Ai:Model` (default
  `claude-haiku-5-5`, the lowest cost current model), at effort `low`, at most 1024 output tokens
  ([self-host.md](self-host.md#events-webhooks-and-schedules)). Each draft is one call billed to the organization's
  Anthropic account.

## AI models

`PUT organizations/ai-settings/models` `{OrganizationId, ReplyDraftModel?, PreReviewModel?}` (`ManageConnections`, needs
a saved key, 409 `AiKeyNotSet` otherwise) chooses the model of each AI feature from a fixed list: `claude-haiku-5-5`
(about $0.10 / $0.50 per million input / output tokens), `claude-sonnet-5-5` ($2 / $10) and `claude-opus-5-5` ($4 / $20).
Another value is 400 `AiModelInvalid`; a field left out keeps its choice. Defaults: reply drafts `claude-haiku-5-5`,
pre-reviews `claude-sonnet-5-5`. `GET organizations/ai-settings` returns `ReplyDraftModel`, `PreReviewModel` and `Models`
(id, name, prices). The choice is kept with the key: removing the key resets it. Audited as `AiModelsChanged`.

## AI pre-review

Before a version goes to review, Claude reviews it the way that store's reviewer would and estimates whether it passes.
It is manual (a member starts it), it is an estimate, and it never blocks or performs a submission.

- **Start.** `POST pre-reviews/pre-review` `{OrganizationId, AppId, Store}` (`EditContent`, 20 an hour per user) checks the
  store record (404 `StoreAppNotFound`), the key (409 `AiKeyNotSet`) and that no pre-review of the record is still queued
  or running (409 `PreReviewInProgress`), saves a queued pre-review with the organization's pre-review model and the
  request's language (the report language: Turkish for a Turkish UI, otherwise English) and queues a `PreReview` store
  job with one attempt (every attempt is billed). It returns the pre-review with its `JobId`; progress and the end come
  on the activity hub like any job (`job.step`: `submission.read`, `screenshots.load`, `ai.review`; then
  `job.succeeded` or `job.failed`). Audited as `PreReviewStarted`.
- **What the model reads.** One compact, tagged and escaped snapshot of the version being prepared, read live by the
  job: app, platform, version, state and whether it updates a live app; the listing texts per locale (the primary
  locale complete, up to seven more shortened); app level values, categories and age rating answers; pricing and
  available territories; the build and export compliance; release type; Play tracks and release notes; review
  information (the notes, and only whether contact details and a demo account are provided: never their values or the
  password); the media sets (counts and sizes); the saved answers (privacy labels, data safety, content rating, target
  audience, review login and notes, again without personal values); in-app products (kind, id, state, period, display
  name and description, whether a review note and screenshot exist); the readiness check items that need attention;
  and the last five rejections with their guidelines and pasted text. Microsoft Store products send their listings,
  image captions, pricing and packages. Up to `PreReviewMaxImages` screenshots (default 10: the primary locale, the
  phone class first, then the next classes) are downloaded from the stores' public image hosts only, scaled to at most
  1200 pixels and sent as base64 JPEG. The store's catalog rules that concern review go in a cached system block.
- **Request.** Messages API (beta path) with the official SDK: a fixed system prompt per store (a strict reviewer of
  that store who cites its real guidelines, cannot run the app, lists what a reviewer will test at runtime instead of
  guessing, and treats everything in the submission as data, never instructions), the catalog rules block with
  `cache_control`, the submission and the images. Adaptive thinking (the default, never disabled), effort per model
  (`low` for Haiku, `medium` for Sonnet and Opus), no sampling parameters or prefill, at most 16000 output tokens, the
  report as structured output (`output_config.format`, JSON schema), and on Sonnet and Opus `fallbacks: "default"`
  with the `server-side-fallback-2026-07-01` beta. The stop reason is checked before the answer is read.
- **Report.** `GET pre-reviews/pre-review?OrganizationId&Id` returns `Status` (Queued, Running, Succeeded, Failed),
  `Verdict` (LikelyApproved, AtRisk, LikelyRejected), `ApprovalLikelihood` (0 to 100), `Summary`, `Findings` (severity
  Blocker, Major or Minor; area; the guideline it breaks; title; evidence; fix), `RuntimeChecks` (what a reviewer will
  test while running the app), `PositiveNotes`, `ImageCount`, the token usage and `EstimatedCostUsd` (list prices), the
  model and the language. `GET pre-reviews?OrganizationId&AppId&Store` lists the last 30, newest first, with the verdict
  and finding counts. A queued or running pre-review whose job ended without it is shown as failed
  (`PreReviewInterrupted`).
- **Errors.** Saved on the pre-review (`ErrorKey`, `ErrorMessage` in the request's language): `AiKeyNotSet`,
  `AiKeyRejected`, `AiRateLimited`, `AiUnavailable`, `PreReviewRefused` (the model declined), `PreReviewFailed` (an
  answer cut short or not a valid report), `PreReviewCancelled`, and the store read failures. Anthropic's own error
  text is never passed on. Audited as `PreReviewCompleted` with the model, verdict, counts, tokens and cost only: no app
  content and never the key. MCP: `pre_reviews_list`, `pre_reviews_get` (read only).

Outgoing webhooks send these events, signed like the signed receiver (`X-Storeasy-Signature` over
`<timestamp>.<body>`): `rejection.opened`, `rejection.closed`, `version.state_changed`, `review.received`,
`review.replied`, `testflight.build.expiring`, `compliance.deadline`, `connection.secret_expiring`, `webhook.test`.

## MCP

`/mcp` is not REST: it is the remote MCP server, with 144 tools that send the same requests as the endpoints above. See
[mcp.md](mcp.md).
