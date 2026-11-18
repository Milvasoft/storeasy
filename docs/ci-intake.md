# CI intake

Build pipelines hand the package (IPA, AAB, APK, MSIX) to storeasy; storeasy reads it, checks it against the rules
catalog and delivers it to a store **test channel** with the app's delivery rule. Production releases and review
submission are never automatic.

> Status: built, server and clients (CLI, GitHub Action, Azure DevOps task) against
> [`tools/CONTRACT.md`](../tools/CONTRACT.md). Tested against fake store servers; the App Store Connect build upload
> (`Api` mode below) still needs one real build to confirm it.

## Ways in

| Way | Identity | Use when |
|---|---|---|
| [GitHub Action](#github-actions) `storeasy/upload-action@v1` | OIDC trusted publisher (no secret) or CI token | GitHub Actions |
| [Azure DevOps task](#azure-pipelines) `StoreasyUpload@1` | Workload identity (OIDC) or CI token | Azure Pipelines |
| [CLI](#cli) `storeasy upload` | CI token, OIDC, or your PAT | Any CI, a laptop |
| [EAS webhook](#eas-build-webhook) | HMAC signature | Expo EAS Build tells storeasy a build is ready; storeasy downloads it |
| [Generic webhook](#generic-webhook) | HMAC signature | Codemagic, Bitrise and other services that publish a download URL |
| Web app and MCP | Your session or PAT | Drag and drop, or the bridge's `artifact_upload` |

Every way ends in the same upload session: the file goes to storage in parts through presigned URLs (resumable), then
a `ProcessArtifact` store job verifies the SHA-256, reads the package, checks it and delivers it. Every step shows up in
live activity and the audit log.

## Who may do what

| Action | Permission (default roles) |
|---|---|
| See packages, sessions, counts, rules and sources | Any member |
| Upload with a PAT, deliver a held package | **Release to test tracks** (Editor and above) |
| CI tokens, trusted publishers, pull sources, delivery rules, intake settings | **Manage store connections** (Admin and Owner) |

Custom roles get these from their permission set. A CI token or an upload token can only upload to its apps.

## Identity

**OIDC trusted publisher (recommended).** Add a rule to the app under **Intake > Trusted publishers**: provider
(GitHub, Azure DevOps, GitLab), repository or project, branch (globs such as `release/*`) and, recommended, an
environment whose reviewers become an extra gate. The CI job presents its own short lived OIDC token (audience
`storeasy`); storeasy checks the signature with the issuer's published keys (allow-listed issuers only) and the claims
against the rule, and returns a 15 minute upload token (`sut_...`) for that one app. Nothing is stored in CI. A rule can
be tried with a pasted token first (**Test**). Azure DevOps tokens carry no branch: restrict them with the service
connection's pipeline permissions and an environment with approvers.

**CI token.** For CI without OIDC: **Intake > CI tokens** creates a token (`sci_...`) bound to the organization and to
chosen apps, scope `artifacts:write`, with an expiry. It is shown once and stored only as a hash. Keep it in the CI's
secret store.

## GitHub Actions

```yaml
permissions:
  contents: read
  id-token: write # OIDC token for storeasy; no secret

jobs:
  android:
    runs-on: ubuntu-latest
    environment: play-internal
    steps:
      - uses: actions/checkout@v4
      - run: ./gradlew bundleRelease
      - uses: storeasy/upload-action@v1
        with:
          url: https://storeasy.example.com # self-hosted instance
          app: 3
          file: app/build/outputs/bundle/release/app-release.aab
          wait: true # fail the job if the package is rejected or the store refuses it
```

Inputs, outputs and the CI token variant: [upload-action README](../tools/upload-action/README.md). Until the action
has its own repository, reference it by path: `uses: <owner>/storeasy/tools/upload-action@<ref>`.

## Azure Pipelines

```yaml
- task: StoreasyUpload@1
  inputs:
    connectedService: storeasy-pusula # Azure Resource Manager connection with workload identity federation
    url: https://storeasy.example.com
    appId: 1
    file: $(Pipeline.Workspace)/ipa/Pusula.ipa
    wait: true
```

Setup of the service connection and the trusted publisher rule: [azure-devops-task README](../tools/azure-devops-task/README.md).

## CLI

```bash
npm install -g storeasy
STOREASY_URL=https://storeasy.example.com STOREASY_TOKEN=<CI token from a secret> \
  storeasy upload app-release.aab --app 3 --wait
```

In GitHub Actions (with `id-token: write`) and Azure Pipelines the CLI uses OIDC on its own when `STOREASY_TOKEN` is
empty. Exit codes: 0 delivered, held or duplicate, 1 usage or network error, 2 package rejected, 3 identity, 4 store
error, 5 wait timed out. Full reference: [CLI README](../tools/storeasy-cli/README.md).

## EAS Build webhook

1. In storeasy, **Intake > Sources > Add > EAS**: pick the app and keep the allowed download domains (`expo.dev`,
   `*.expo.dev`, `storage.googleapis.com`). storeasy shows the source URL (`/api/webhooks/eas/{sourceId}`) and a
   secret, once.
2. Register it with EAS:

   ```bash
   eas webhook:create --event BUILD --url https://storeasy.example.com/api/webhooks/eas/<sourceId> --secret <secret>
   ```

When a build finishes, EAS calls storeasy; storeasy checks the `expo-signature` header (HMAC-SHA1 of the raw body),
downloads `artifacts.buildUrl` over HTTPS only from the allowed domains (redirects included, internal addresses
refused) and continues like an upload. An invalid signature is refused (401) and still recorded.

## Generic webhook

For services that publish a download URL. **Intake > Sources > Add > Webhook** gives a URL
(`/api/webhooks/artifacts/{sourceId}`), a secret and the allowed download domains. The service sends storeasy's own
payload (`storeasy.artifact-ready/v1`) with at least the download URL and the file's `sha256`, signed:

- `X-Storeasy-Timestamp`: Unix time of the call; older than 5 minutes is refused (replay protection);
- `X-Storeasy-Signature: sha256=<hex>`: HMAC-SHA256 with the source secret over `<timestamp>.<raw body>`.

A downloaded file whose SHA-256 differs from the declared one is rejected. Both receivers take bodies up to 1 MiB and
answer 202 with the session when they accept a package. The API also answers the same receivers at
`/webhooks/eas/...` and `/webhooks/artifacts/...`; the web container proxies both forms.

## Checks

Before anything reaches a store, storeasy checks:

- the package identifier matches the app (bundle id, package name, MSIX identity);
- Android: target API level from the rules catalog, a version code higher than any seen before, the upload key
  signature;
- Apple: Xcode, SDK and deployment target minimums from the rules catalog, a build number not used before, a
  distribution signature, export compliance.

A failed check rejects the package (`Rejected`, CLI exit code 2). A warning passes, and the delivery rule decides
whether it holds the package.

## Delivery rules

Each app has one delivery rule per platform: on or off, a target (test channels only), what to do on a check warning
(**Hold**, the default, or **Deliver**) and, for a Play track, an optional staged rollout fraction.

| Platform | Target | What storeasy does |
|---|---|---|
| iOS | An internal TestFlight group | Uploads the IPA to App Store Connect (or waits for it, see [Apple upload modes](#apple-upload-modes)), waits for Apple's processing by retrying, then adds the build to the group. |
| Android | A Play testing track (`internal`, `alpha`, `beta` or a closed track) | Opens a Play edit, uploads the App Bundle, releases it on the track (staged when a fraction is set) and commits the edit. |
| Windows | The app's draft submission or a package flight | Adds the MSIX package to a draft submission of the app or of the flight. The draft is not sent to certification: a member submits it (Submit for review permission, Store id typed as confirmation). |

- An upload can override the target for itself (`track`, `group`), but only with test channels: production tracks,
  external TestFlight groups and review submission are refused (`DeliveryTargetNotAllowed`).
- The same file again (same SHA-256, for example a re-run) is recorded as a duplicate: nothing is uploaded or delivered
  twice, and the result of the first upload is reported.

## Held packages

A package that passed its checks stops in `Held` (CLI exit code 0) instead of being delivered when:

- the app has no delivery rule for the platform, or the rule is off;
- the checks passed with warnings and the rule says **Hold**;
- it is an APK (Google Play takes App Bundles; APKs are never delivered automatically);
- the upload asked to be held for confirmation (`delivery.holdForConfirmation: true`). The web app's manual upload
  always does this, so the checks are shown before anything reaches a store.

A member with **Release to test tracks** delivers a held package by hand in the web app, with
`POST /api/v1.0/artifacts/session/deliver` or the MCP tool `artifacts_deliver_held`. The target stays a test channel;
a held package can also be deleted.

## Counts and history

**Intake** lists every upload session with its source (CI, CLI, webhooks, manual, or seen by the build sync), steps,
checks, target and CI run. `GET /api/v1.0/artifacts/counts` counts the packages by status and by source over a period
(default the last 30 days, at most 366 days), for the organization or one app; `GET /api/v1.0/artifacts/summary` shows
the last package of every app with its delivery rules. MCP: `artifacts_summary`, `artifacts_sessions`,
`artifacts_session_get`.

Packages are kept for the organization's retention (1 to 365 days, default 30, under the intake settings) and then
deleted with their files; the session history stays. The largest package accepted is 4 GiB unless the organization sets
a lower limit.

## Apple upload modes

The instance setting `Storeasy:Intake:AppleUploadMode` (`.env`: `STOREASY_APPLE_UPLOAD_MODE`, see
[self-host.md](self-host.md#package-intake)) decides how an IPA reaches App Store Connect:

| Mode | What happens |
|---|---|
| `Api` (default) | storeasy uploads the IPA itself with the App Store Connect build upload API (`buildUploads`, `buildUploadFiles`, presigned parts streamed from storage), then follows the upload's state until the build exists. |
| `External` | The CI uploads to Apple itself (EAS Submit, Xcode Cloud, Transporter, `xcrun altool`). storeasy still receives the IPA (or the webhook), runs the checks, uploads nothing, waits until a build with the package's version and build number appears in App Store Connect, then applies the delivery rule. |

`External` is the fallback while the `Api` path has not been confirmed with a real build.

## Never automatic

- Releasing to a Play production track or a staged production rollout.
- Submitting an App Store version for review, or releasing it.
- Adding a build to an external TestFlight group (beta review).
- Sending a Microsoft Store submission to certification or publishing it.

These stay in storeasy behind the matching permission (Admin and Owner by default) and a typed confirmation, in the
web app or through MCP.
