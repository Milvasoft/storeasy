import { publicOrigin } from '@/features/mcp/snippets'

// The intake setup snippets (design 13.19-13.22, docs/ci-intake.md), filled with the app id and this installation's
// address in the browser. None carries a secret: OIDC needs none and the token paths read STOREASY_TOKEN.

export function githubWorkflow(appId: number, environment?: string | null): string {
  return `name: release
on:
  push:
    branches: [main]

permissions:
  contents: read
  id-token: write # OIDC token for storeasy; no secret

jobs:
  release:
    runs-on: ubuntu-latest${environment ? `\n    environment: ${environment}` : ''}
    steps:
      - uses: actions/checkout@v4
      - run: ./gradlew bundleRelease
      - uses: Milvasoft/storeasy/tools/upload-action@v0
        with:
          url: ${publicOrigin()}
          app: ${appId}
          file: app/build/outputs/bundle/release/app-release.aab
          wait: true # fail the job if the package is rejected or the store refuses it`
}

export function azurePipeline(appId: number, serviceConnection?: string | null, environment?: string | null): string {
  return `trigger:
  branches:
    include: [main]

stages:
  - stage: deliver
    jobs:
      - deployment: upload
        environment: ${environment || 'testflight'} # approvals live here
        pool:
          vmImage: macos-15
        strategy:
          runOnce:
            deploy:
              steps:
                - task: StoreasyUpload@1
                  inputs:
                    connectedService: ${serviceConnection || '<service connection>'}
                    url: ${publicOrigin()}
                    appId: ${appId}
                    file: $(Pipeline.Workspace)/ipa/App.ipa
                    wait: true`
}

export function cliCommand(appId: number): string {
  return `npm install -g storeasy
STOREASY_URL=${publicOrigin()} STOREASY_TOKEN=<CI token from a secret> \\
  storeasy upload app-release.aab --app ${appId} --wait`
}

export function restSteps(appId: number): string {
  const api = `${publicOrigin()}/api/v1.0`
  return `# 1. open a session
curl -X POST ${api}/apps/${appId}/artifacts \\
  -H "Authorization: Bearer $STOREASY_TOKEN" \\
  -H "Content-Type: application/json" \\
  -d '{"fileName":"app-release.aab","size":51485696,"sha256":"<sha256 hex>"}'
# answer: sessionId, partSize, parts[].url (or duplicate: true)

# 2. PUT every part to its URL and keep the ETag
curl -X PUT "$PART_URL" --data-binary @part-01 -D - | grep -i etag

# 3. close it
curl -X PUT ${api}/artifacts/<sessionId>/complete \\
  -H "Authorization: Bearer $STOREASY_TOKEN" \\
  -H "Content-Type: application/json" \\
  -d '{"parts":[{"partNumber":1,"eTag":"<etag>"}]}'`
}

export function easCommand(url: string, secret: string): string {
  return `eas webhook:create --event BUILD \\
  --url ${url} \\
  --secret ${secret}`
}

export function webhookSample(url: string, appId: number): string {
  return `POST ${url}
X-Storeasy-Timestamp: 1763474520
X-Storeasy-Signature: sha256=<hex HMAC-SHA256 of "<timestamp>.<body>">
Content-Type: application/json

{
  "schema": "storeasy.artifact-ready/v1",
  "appId": ${appId},
  "platform": "android",
  "fileName": "app-release.aab",
  "downloadUrl": "https://api.codemagic.io/artifacts/...",
  "sha256": "<sha256 hex>",
  "size": 51485696,
  "source": { "provider": "codemagic", "runId": "6738f1c2a4", "runUrl": "https://codemagic.io/app/.../build/6738f1c2a4" }
}`
}

export const EAS_DOMAINS = ['expo.dev', '*.expo.dev', 'storage.googleapis.com']

/** IntakeRules.IsValidDomainEntry, roughly: a DNS name with a dot, optionally starting with "*.". */
export function isDomainEntry(entry: string): boolean {
  const host = entry.startsWith('*.') ? entry.slice(2) : entry
  return host.length <= 253 && /^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z][a-z0-9-]{0,61}[a-z0-9]$/i.test(host)
}

export function parseDomains(text: string): string[] {
  return text
    .split(/[\s,]+/)
    .map((entry) => entry.trim().toLowerCase())
    .filter(Boolean)
}
