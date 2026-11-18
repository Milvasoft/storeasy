# CI intake

Build pipelines hand the package (IPA, AAB, APK, MSIX) to storeasy; storeasy reads it, checks it against the rules
catalog and delivers it to a store **test channel** with the app's delivery rule. Production releases and review
submission are never automatic.

> Status: the client side (CLI, GitHub Action, Azure DevOps task) is ready in [`tools/`](../tools/README.md); the
> server side is being built (plan phase 3) to the contract in [`tools/CONTRACT.md`](../tools/CONTRACT.md).

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
storeasy parses, checks and delivers it. Every step shows up in live activity and the audit log.

## Identity

**OIDC trusted publisher (recommended).** Add a rule to the app under **Intake > Trusted publishers**: provider
(GitHub, Azure DevOps, GitLab), repository or project, branch (globs such as `release/*`) and, recommended, an
environment whose reviewers become an extra gate. The CI job presents its own short lived OIDC token (audience
`storeasy`); storeasy checks the signature and the claims against the rule and returns a 15 minute upload token for that
one app. Nothing is stored in CI. Azure DevOps tokens carry no branch: restrict them with the service connection's
pipeline permissions and an environment with approvers.

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
empty. Exit codes: 0 delivered or duplicate, 1 usage or network error, 2 package rejected, 3 identity, 4 store error,
5 wait timed out. Full reference: [CLI README](../tools/storeasy-cli/README.md).

## EAS Build webhook

1. In storeasy, **Intake > Sources > Add > EAS**: pick the app and keep the allowed download domains (`expo.dev`,
   `*.expo.dev`, `storage.googleapis.com`). storeasy shows the source URL (`/webhooks/eas/{sourceId}`) and a secret,
   once.
2. Register it with EAS:

   ```bash
   eas webhook:create --event BUILD --url https://storeasy.example.com/webhooks/eas/<sourceId> --secret <secret>
   ```

When a build finishes, EAS calls storeasy; storeasy checks the `expo-signature` header (HMAC-SHA1 of the raw body),
downloads `artifacts.buildUrl` only from the allowed domains (redirects included) and continues like an upload. An
invalid signature is refused (401) and still recorded.

## Generic webhook

For services that publish a download URL. **Intake > Sources > Add > Webhook** gives a URL
(`/webhooks/artifacts/{sourceId}`), a secret and the allowed download domains. The service sends storeasy's own
payload (`storeasy.artifact-ready/v1`) with at least the download URL and the file's `sha256`, signed:

- `X-Storeasy-Timestamp`: Unix time of the call; older than 5 minutes is refused (replay protection);
- `X-Storeasy-Signature: sha256=<hex>`: HMAC-SHA256 with the source secret over `<timestamp>.<raw body>`.

A downloaded file whose SHA-256 differs from the declared one is rejected.

## Delivery rules

Each app has a delivery rule per platform, for example:

| Platform | Default target |
|---|---|
| iOS | TestFlight internal group |
| Android | Play `internal` track |
| Windows | Microsoft Store package flight |

- An upload can override the target for itself (`track`, `group`), but only with test channels: production tracks,
  external TestFlight groups and review submission are refused (`DeliveryTargetNotAllowed`).
- A warning from the checks can hold the delivery for a person to approve (status `Held`, exit code 0).
- The same file again (same SHA-256, for example a re-run) is recorded as a duplicate: nothing is uploaded or delivered
  twice, and the result of the first upload is reported.

What storeasy checks before delivery: the package identifier matches the app (bundle id, package name, MSIX identity),
the version code is higher than the previous one, the target API level and SDK and Xcode minimums from the rules
catalog, and the signature.

## Never automatic

- Releasing to a Play production track or a staged production rollout.
- Submitting an App Store version for review, or releasing it.
- Adding a build to an external TestFlight group (beta review).
- Publishing a Microsoft Store submission to everyone.

These stay in storeasy behind the Admin or Owner role and a typed confirmation, in the web app or through MCP.
