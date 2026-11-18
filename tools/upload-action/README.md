# storeasy upload action

Uploads an IPA, AAB, APK or MSIX from a GitHub Actions workflow to storeasy. storeasy reads the package, checks it
against the store rules and delivers it with the app's delivery rule (TestFlight internal group, Play test track,
Microsoft package flight). Production and review submission are never automatic.

Identity is OIDC (trusted publisher): the job shows GitHub's short lived OIDC token (audience `storeasy`), storeasy
checks it against the app's rule (repository, branch, environment, optional workflow) and gives back a 15 minute upload
token for that app. Nothing is stored in GitHub secrets.

Runtime: `node24`. The action is one bundled file (`dist/index.cjs`), built from `src/` and the `storeasy` package in
`../storeasy-cli`.

## Setup

1. In storeasy: Intake > Trusted publishers > Add rule. Provider GitHub, the app, `owner/repo`, branch (globs like
   `release/*` work) and, recommended, a GitHub environment whose reviewers become an extra gate.
2. In the workflow, give the job `id-token: write` and add the step:

```yaml
name: release
on:
  push:
    branches: [main]

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
          app: 3
          file: app/build/outputs/bundle/release/app-release.aab
          wait: true # fail the job if the package is rejected or the store refuses it
```

Until the action has its own repository, reference it from the storeasy repository by path:
`uses: <owner>/storeasy/tools/upload-action@<ref>`.

### Without OIDC

Create a CI token (`sci_...`) in storeasy under Intake > CI tokens, store it as a secret and pass it:

```yaml
      - uses: storeasy/upload-action@v1
        with:
          app: 3
          file: build/ios/ipa/Pusula.ipa
          token: ${{ secrets.STOREASY_TOKEN }}
```

## Inputs

| Input | Default | |
|---|---|---|
| `app` (or `app-id`) | | storeasy app id. Required. |
| `file` | | Package path, relative to the workspace. Required. |
| `url` | `https://storeasy.milvasoft.com` | Base URL of a self-hosted storeasy. |
| `store` | | `apple`, `google` or `microsoft`; checked against the package. |
| `track` | | Play test track; overrides the delivery rule for this upload. |
| `group` | | TestFlight internal group; overrides the delivery rule for this upload. |
| `notes` | | Release notes / What to Test. |
| `wait` | `false` | Wait for the delivery and fail the step on rejection or store error. |
| `timeout-minutes` | `30` | Limit for `wait`. |
| `token` | | CI token for workflows without OIDC. Empty: OIDC. |

## Outputs

`session-id`, `duplicate`, `original-session-id`, `sha256`, `status`, `summary`, `store-job-id`, `session-url`,
`exit-code` (0 delivered or duplicate, 2 rejected, 3 identity, 4 store error, 5 wait timed out). A short result is
also written to the job summary.

## Behaviour

- Same file twice (same SHA-256, for example a re-run): nothing is uploaded or delivered again, the step succeeds and,
  with `wait`, reports the first upload's result.
- Parts are retried; a re-run of the job resumes an interrupted upload.
- The OIDC token and the upload token are registered with `::add-mask::` and never printed otherwise.

## Development

```bash
cd tools
npm install
npm run build -w upload-action   # rebuilds dist/index.cjs; commit it
npm test -w upload-action
```
