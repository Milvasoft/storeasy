# StoreasyUpload@1 (Azure DevOps task)

Uploads an IPA, AAB, APK or MSIX from Azure Pipelines to storeasy. storeasy reads the package, checks it and delivers
it with the app's delivery rule to a test channel (TestFlight internal group, Play test track, Microsoft package
flight). Production and review submission are never automatic.

Identity, in order of preference:

1. **Workload identity (OIDC), no stored secret.** The task asks Azure DevOps for the OIDC token of an Azure Resource
   Manager service connection that uses workload identity federation, and exchanges it with storeasy for a 15 minute
   upload token for the app. storeasy matches the token's subject `sc://<organization>/<project>/<connection>`
   against the app's trusted publisher rule.
2. **CI token** (`sci_...`) from a secret variable, for pipelines without a suitable service connection.

The task is one bundled file (`StoreasyUploadV1/dist/index.cjs`, Node 20 handler), built from `src/` and the
`storeasy` package in `../storeasy-cli`. It needs no task library at run time.

## Setup with workload identity

1. Azure DevOps: Project settings > Service connections > New > Azure Resource Manager > Workload identity federation
   (manual). Name it, for example `storeasy-pusula`. Turn off "Grant access permission to all pipelines" and allow only
   the release pipeline.
2. storeasy: Intake > Trusted publishers > Add rule > Azure DevOps: organization, project, service connection name.
   The Azure DevOps token carries no branch; restrict branches with the service connection's pipeline permissions and
   an environment with approvers.
3. Pipeline:

```yaml
trigger:
  branches:
    include: [main]

stages:
  - stage: deliver
    jobs:
      - deployment: testflight
        environment: testflight # approvals here
        pool:
          vmImage: macos-15
        strategy:
          runOnce:
            deploy:
              steps:
                - task: StoreasyUpload@1
                  inputs:
                    connectedService: storeasy-pusula
                    appId: 1
                    file: $(Pipeline.Workspace)/ipa/Pusula.ipa
                    wait: true
```

## Setup with a CI token

Create a CI token in storeasy (Intake > CI tokens), store it as a secret pipeline variable `STOREASY_TOKEN`, and:

```yaml
- task: StoreasyUpload@1
  inputs:
    appId: 3
    file: app/build/outputs/bundle/release/app-release.aab
    token: $(STOREASY_TOKEN)
    track: internal
    wait: true
```

## Inputs

| Input | | |
|---|---|---|
| `connectedService` (alias `serviceConnection`) | | Service connection with workload identity. Empty: use `token`. |
| `appId` | required | storeasy app id. |
| `file` | required | Package path. |
| `url` | `https://storeasy.milvasoft.com` | Base URL of a self-hosted storeasy. |
| `token` | | CI token from a secret variable. |
| `store` | | `apple`, `google`, `microsoft`; checked against the package. |
| `track` | | Play test track; overrides the delivery rule for this upload. |
| `group` | | TestFlight internal group; overrides the delivery rule for this upload. |
| `notes` | | Release notes / What to Test. |
| `wait` | `false` | Follow the delivery; the task fails on rejection or store error. |
| `timeoutMinutes` | `30` | Limit for `wait`. |

Output variables (use `name:` on the step to read them in later jobs): `StoreasySessionId`, `StoreasyDuplicate`,
`StoreasyStatus`, `StoreasyExitCode` (0 delivered or duplicate, 2 rejected, 3 identity, 4 store error, 5 wait timed
out), `StoreasySessionUrl`.

The OIDC token, the upload token, the CI token and `System.AccessToken` are registered with
`##vso[task.setsecret]` and never printed.

## Build and package

```bash
cd tools
npm install
npm run build -w azure-devops-task   # rebuilds StoreasyUploadV1/dist/index.cjs; commit it
npm test -w azure-devops-task
# Packaging for the Marketplace (not done here): npx tfx-cli extension create --manifest-globs vss-extension.json
```

Before publishing, set `publisher` in `vss-extension.json` to the Marketplace publisher id.
