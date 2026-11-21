# Connecting stores

storeasy talks to the stores only through their official APIs, with credentials your organization adds under
**Organization > Connections**. Adding, verifying and deleting a connection needs the **Manage store connections**
permission (Owner and Admin, or a custom role that has it) and a verified e-mail address. An organization can have
several connections per store (for example two Apple developer accounts).

Each connection can have an optional icon, picked when it is added and changed later with **Edit** (with its name): the
store's own logo or a colored generic icon. Records that come from that connection show the icon in place of the
`AS` / `GP` / `MS` / `WG` code; places that know only the store (filters, counts, store columns) use the icon of the store's
oldest connection that has one. Without an icon the code stays. Store logos are trademarks of their owners and only
identify the store; each is offered only for its own store.

## What storeasy keeps

| Kept | Never |
|---|---|
| The credential, sealed with AES-256-GCM envelope encryption (a data key per connection, wrapped by the instance master key) | Returned by any endpoint, MCP tool, export or log |
| A fingerprint shown in the app: App Store Connect key id and the last 4 characters of the issuer id; Google service account e-mail and project id; Microsoft seller id, the last 4 characters of the tenant and client ids and the secret's expiry date; winget fork, its owner and the token kind (and the expiry you entered) | Your Apple ID, Google account, Partner Center or GitHub password |
| Last verification time and result | Copies of the credential in jobs, plans or audit entries |

Only the store job runner, the verify command and replacing a key open a credential, in memory, for the duration of the
call. See [self-host.md](self-host.md#7-master-key-backup-and-rotation) for the master key.

## Replacing a key

An Apple, Google or winget key is replaced in place with **Replace key** on the connection's card (`PUT
storeconnections/connection/key`, Manage store connections permission and a verified e-mail address). The connection
keeps its id, so its apps, store records, jobs, plans and history stay. Typical case: the connection uses an App Manager
key and the App Store analytics reports need an Admin key; Apple does not change a key's role, so create a new Admin key
and replace the old one with it.

- **Apple:** the new key must belong to the same App Store Connect team: storeasy compares its issuer id with the one
  sealed in the connection and refuses another team's key (`StoreKeyIssuerMismatch`). The key id may change.
- **Google:** any service account JSON key is taken; it may be another service account or another Cloud project.
- **winget:** a new GitHub token for the same fork; another fork is refused (`StoreKeyAccountMismatch`). Enter the new
  token's expiry date with it.

storeasy runs the same offline checks as when adding a connection, then verifies the new key against the store. Only
when the store accepts it is it sealed in place of the old one, with the new fingerprint and the connection marked
verified. When the store refuses it (`StoreKeyRejected`, followed by the store's answer) nothing changes: the old key,
fingerprint and status stay. The change is audited as *Store connection key replaced* with the old and new fingerprint,
never the key. Revoke the old key in App Store Connect or Google Cloud afterwards. A Microsoft client secret is renewed
in place the same way with **Renew secret** (see below).

## App Store Connect

You need an App Store Connect API **team key**.

1. In App Store Connect, open **Users and Access > Integrations > App Store Connect API**. The Account Holder has to
   request API access once.
2. Under **Team Keys**, generate a key. Role: **App Manager**. It covers what storeasy does: app information and
   version texts, screenshots and previews, builds and export compliance, TestFlight, pricing and availability, age
   rating, review information, submission and customer review replies. **Developer** is not enough for metadata.
   Checked live on 2026-11-18: an App Manager team key worked for metadata, screenshots, builds, age rating and
   subscriptions (including review screenshots). Registering App Store Connect webhooks may need an **Admin** key (not
   tested yet); everything else works with App Manager.
3. Download the `.p8` file (Apple lets you download it once) and note the **Key ID** and the **Issuer ID** shown on the
   page.
4. In storeasy, add an Apple connection with the issuer id, key id and the content of the `.p8` file, then verify it.
5. Run **Sync** to list the account's apps. Apps with the same bundle id in another store are paired automatically.

Apple does not let the API create apps: create the app record in App Store Connect first.

**Webhooks.** Under **Webhooks**, add an App Store Connect endpoint for the connection. storeasy registers one App Store
Connect webhook per App Store app of the connection (apps that arrive with a later sync get one too) for the version
state (App Review), build processing (`BUILD_UPLOAD_STATE_UPDATED`) and beta review
(`BUILD_BETA_DETAIL_EXTERNAL_BUILD_STATE_UPDATED`) events, all with the endpoint's address and secret. **Ping** asks
App Store Connect to send a signed test event to each webhook; the pings show in the event log with their signature
verdict.

## Google Play

You need a Google Cloud service account with a JSON key, invited to your Play Console account.

1. In Google Cloud, pick or create a project, enable the **Google Play Android Developer API**, create a service
   account and create a **JSON key** for it. The service account needs **no Google Cloud roles**. If your organization
   policy blocks key creation (`iam.disableServiceAccountKeyCreation`), allow it for this project.
2. In Play Console, open **Users and permissions > Invite new users**, enter the service account e-mail and grant these
   app permissions (the set checked live on 2026-11-18):
   - **View app information and download bulk reports (read-only)**;
   - **Release apps to testing tracks**;
   - **Manage store presence**;
   - **Reply to reviews**.

   Play may add related permissions on its own (for example quality views and policy declarations); that is fine. Reads
   work right away. Optional, off by default:
   - **Release to production, exclude devices, and use Play App Signing**: only if storeasy should release to
     production; storeasy still requires the Admin or Owner role and a typed confirmation;
   - **View financial data**: only if you want storeasy to read sales and subscription reports.

   Play Console labels change over time; pick the closest match.
3. In storeasy, add a Google connection with the content of the JSON key file, then verify it.
4. Play has no API to list apps: add each app by its package name. storeasy proves access by opening and discarding an
   edit, and removes a record Play refuses.

Create the app in Play Console first. Google can require the first bundle of a new app to be uploaded in Play Console.

**Purchase notifications (Real-time developer notifications).** Play publishes subscription, one-time purchase,
voided purchase, pending refund review and test notifications to a Cloud Pub/Sub topic; storeasy receives them through
a push subscription and checks the push token Google signs (issuer Google, audience = the receiver address, e-mail = the
push service account). No secret is shared.

1. In Google Cloud (the project of your service account, or any project), enable the **Cloud Pub/Sub API** and create a
   topic, for example `play-rtdn`.
2. On the topic, grant `google-play-developer-notifications@system.gserviceaccount.com` the **Pub/Sub Publisher** role.
3. Pick the service account the push requests authenticate as (a new one, for example `play-rtdn-push`, needs no
   roles). If the project was created before April 2021, give the Pub/Sub service agent
   (`service-<project number>@gcp-sa-pubsub.iam.gserviceaccount.com`) the **Service Account Token Creator** role.
4. In storeasy, under **Webhooks**, add a **Google Play notifications** endpoint with that service account's e-mail and
   copy the address it shows (`/api/webhooks/google/{id}`).
5. Create a **push** subscription on the topic with that address as the endpoint, **Enable authentication** on, the
   service account from step 3, and the audience left empty (Pub/Sub then uses the address) or set to the address.
6. In Play Console, per app, open **Monetize with Play > Monetization setup**, enter the full topic name
   (`projects/<project>/topics/play-rtdn`), choose the notification types (subscriptions and voided purchases, and
   all one-time products if you sell them), save, and press **Send test notification**. The test arrives in the event
   log as `TEST_NOTIFICATION`.

storeasy stores the decoded notification with purchase and refund tokens masked, keyed by the Pub/Sub message id so a
redelivered message is stored once.

## Microsoft Store

You need a Microsoft Entra application associated with your Partner Center account. One connection covers both
Microsoft APIs storeasy uses: the submission API for MSIX apps (listings, images, packages, package flights, add-ons,
pricing, gradual rollout) and the API for MSI/EXE apps (installer URLs, listings).

1. In Partner Center, open **Account settings > User management > Microsoft Entra applications** and add (or create)
   an application with the **Manager** role. A lesser role cannot create or commit submissions.
2. Create a client secret for it (in Partner Center on the application's page, or in the Microsoft Entra admin center
   under **App registrations > Certificates and secrets**). A secret lives **at most 24 months**; Microsoft recommends
   12. Note the secret's **value** (not its id) and its expiry date.
3. Note the **tenant ID** and the application's **client ID** (both GUIDs) and your **seller ID** (a number, under
   Partner Center account settings).
4. In storeasy, add a Microsoft connection: paste
   `{"tenantId": "...", "clientId": "...", "clientSecret": "...", "sellerId": "..."}` (the web app has a form for it)
   and enter the secret's expiry date. storeasy refuses an expiry in the past or more than 24 months away.
5. Verify it. storeasy takes a token for both APIs and reads one application; a wrong id, a wrong secret or a missing
   Partner Center association answers as rejected.
6. Run **Sync**: MSIX apps of the account come in on their own. MSI/EXE apps have no list call: add each one by its
   Store id (`ms/product`).

**Secret expiry.** storeasy keeps the expiry date and reminds the organization's owners and admins 30, 7 and 1 days
before it (in-app, e-mail, web push and the outgoing webhook event `connection.secret_expiring`). Replace the secret in
place before it expires: create a new secret for the same application and enter it with its expiry date under the
connection (`PUT ms/connection/secret`). storeasy verifies the new secret against Microsoft first and keeps the old one
if that fails; the tenant, client and seller must stay the same.

**What only Partner Center can do.** Microsoft's APIs can only change an app after its first submission was made in
Partner Center, and a submission created through the API but then edited in Partner Center can no longer be changed or
committed through the API. Store-managed consumable add-ons and products with mandatory updates answer 409, and the
price of a product on Pricing Version 2 cannot be read or written through the API. storeasy shows each of these as an
assisted step with a Partner Center link instead of failing silently.

Writes go through change plans like the other stores (`ms/change`), and sending a submission to certification needs the
**Submit for review** permission and the product's Store id typed as confirmation.

**No status webhooks.** Partner Center has no webhooks for Store app submission or certification state (its commercial
marketplace webhooks are a different product); it e-mails the account. storeasy reads submission states with the
periodic Microsoft sync instead.

## winget

winget (Windows Package Manager) has no store account or submission API: packages are YAML manifests in the community
repository [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) on GitHub, and a new version goes in as a
pull request that the validation pipeline checks and community moderators merge. storeasy works on your behalf with a
GitHub token and **your fork** of winget-pkgs: it reads manifests, plans the pull request, and after the plan is approved
pushes a branch to the fork and opens the pull request.

1. Fork [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) with the **Fork** button (on your user or on
   an organization).
2. Create a token: **GitHub > Settings > Developer settings > Personal access tokens > Tokens (classic) > Generate new
   token**, scope **public_repo**, with an expiry date you note. A fine-grained token (`github_pat_...`) can push the
   branch to the fork, but GitHub does not let it open a pull request in a repository its owner does not own; storeasy
   then stops after pushing the branch and gives you the compare page to open the pull request yourself. A GitHub App is
   not supported: its installation token cannot open a pull request in microsoft/winget-pkgs either.
3. In storeasy, add a **winget** connection: the token and the fork as `owner/name` (the web app has a form; the secret
   is `{"token": "...", "fork": "owner/winget-pkgs"}`), optionally the token's expiry date.
4. Verify it. storeasy reads `GET /user` (and, for a classic token, checks that its scopes include `public_repo` or
   `repo`) and the fork (it must be a fork of microsoft/winget-pkgs that the token can push to).
5. Add each package by its identifier (`Publisher.Package`, **Apps > Add a winget package** or `winget/package`): winget
   has no API that lists a publisher's packages. Attach it to the app of the same program (usually its Microsoft Store
   MSI/EXE product) or to a new app. A package that is not in winget-pkgs yet can be added too; its first pull request is
   a "New package".

The fingerprint is the fork, its owner and the token kind (classic or fine-grained); the token is sealed like every
store secret and never shown. **Replace key** takes a new token for the same fork (another fork is refused with
`StoreKeyAccountMismatch`) and checks it with GitHub first.

**What storeasy reads.** The version folders of the package (`manifests/<letter>/<Publisher>/<Package>/<version>`), the
manifests of a version (names, descriptions, release notes and tags per locale: the winget "listing"; the installers with
their addresses, hashes, scopes and switches), and the fork owner's pull requests for the package with their labels,
check runs and the bot's and moderators' comments. Reads use the token for GitHub's higher rate limit; winget-pkgs is
public.

**What storeasy writes, always as a change plan** (`winget/change`, dry run, approved under **Plans**):

- **New version**: the latest version's manifests are copied, the version and the installers replaced, release notes and
  version specific fields (release date, product codes, hashes) dropped unless given again. Installers are given in the
  plan or taken from the app's Microsoft Store MSI/EXE product (address, MSI or EXE, architectures, silent switches;
  one installer per architecture). The approval needs **Submit for review** and the package identifier and version typed
  as confirmation. Applying it downloads every installer from its public address and writes its SHA-256 into the
  installer manifest (a hash given in the plan must match the download, otherwise nothing is pushed), checks the
  manifests again, brings the fork's default branch up to date, pushes one commit on a `storeasy/...` branch and opens
  the pull request with the repository's checklist.
- **Locale texts** of an existing version: only the changed locale manifests are pushed (an "Update metadata" pull
  request).

Before a plan is saved storeasy checks the manifests against the manifest schema (storeasy writes schema 1.12.0, the
version wingetcreate writes) and the repository's policies it can check without running the installer: multi-file
manifests only, one version per pull request, no version that already exists and no second open pull request for it,
https installer addresses on the publisher's own site (a warning when the host differs from the publisher's), EXE
installers with silent switches, no duplicate installer entries, allowed installer types. The same checks show on the
package page and in **Readiness**. Running the installer, the virus scan and SmartScreen are the validation pipeline's.

**Review state.** Each pull request has a state read from GitHub: *Validating*, *Validation failed* (an error label such
as `Validation-Error`, `Error-Hash-Mismatch` or a failed check), *Needs author feedback* (`Needs-Author-Feedback`,
`Changes-Requested`, `Needs-CLA`), *Validation completed* (`Validation-Completed`, `Azure-Pipeline-Passed`),
*Approved* (`Moderator-Approved`), *Published* (merged) and *Closed*. A waived label (`Waived-X`) cancels `X`; pipeline
internal errors are retried by the moderators and do not count as a failure. The schedule reads open pull requests every
15 minutes (`winget/sync` reads one package at once); a state change is audited and notified like a store version
state (in-app, e-mail, web push, the `version.state_changed` webhook), and a merge reads the package again. The first
pull request of a GitHub account asks for the Microsoft Contributor License Agreement; sign it on the pull request
(an assisted step reminds you).

**Not in winget.** There is no store page, no ratings or reviews, no install statistics and no pricing: storeasy shows
none of them for winget. Manifests have no screenshots. Removing a version and changing an existing version's installers
are not offered (moderators handle removals). Expiry reminders cover Microsoft client secrets only; a winget token's
expiry date is shown on the connection.

## Store page performance

The app's **Store performance** tab shows impressions, page views, downloads and the conversion rate (downloads divided
by page views) per day, store, country and traffic source. storeasy reads them once a day (05:30 UTC) and on **Fetch
now**; reads never change anything in a store. The first read goes back as far as each source allows, later reads repeat
the last 7 days because the stores complete recent days late. Daily rows are kept for 25 months.

| Store | Source | What the owner sets up |
|---|---|---|
| App Store | Analytics Reports API: "App Store Discovery and Engagement" (impressions, product page views) and "App Downloads" (first-time downloads, redownloads), standard reports, daily | Press **Enable analytics reports** once per app (Manage connections permission, a confirmation, audited). This is the one store write: it creates an ONGOING report request at Apple. Creating it needs an API key with the **Admin** role; reading the reports needs **Admin** or **Sales and Reports**. Apple delivers the first data one or two days later and every day after that with a delay of one to two days. Apple stops a request nobody reads for a long time; storeasy then shows it as not enabled again. |
| Google Play | Play Console statistics exported to Cloud Storage: `stats/store_performance/store_performance_<package>_<yyyyMM>_country.csv` and `..._traffic_source.csv` (store listing visitors, acquisitions) | Enter the reports bucket on the tab (Play Console, **Download reports**, **Copy Cloud Storage URI**, e.g. `gs://pubsite_prod_rev_01234567890987654321`). The service account must be a Play Console user with **View app information and download bulk reports** set for the whole account (global). storeasy reads it with the `devstorage.read_only` scope; a refusal shows as "No access" on the tab. The first read takes the last 6 monthly files. Play reports no impressions. |
| Microsoft Store | Partner Center analytics API: `analytics/appacquisitions` (daily acquisitions by market and store client) and `analytics/funnel` (page views, one call per day) | Nothing new: the existing Entra app is used. It needs a Partner Center role that can read analytics (Manager or Developer); a 401 or 403 shows as "No access". The first read takes the last 90 days. Microsoft reports no impressions and counts funnel page views as users. |

Conversion is computed only over the rows that report both page views and downloads, so one store's downloads are
never divided by another store's page views. The country filter and the source breakdown are separate slices of the
same data; Google Play reports countries and traffic sources in separate files, so a country filter with a source
breakdown shows only the stores that report both together.

## Troubleshooting

| Symptom | Check |
|---|---|
| Verify fails with 401 for Apple | Key revoked, wrong issuer id, or the `.p8` content was changed (keep the `BEGIN` and `END` lines). |
| Verify fails with 403 for Google | The service account was not invited in Play Console, or the API is not enabled in its Cloud project. |
| A Play app disappears after sync | Play refused the service account for that package: grant it access to the app. |
| Verify fails for Microsoft | The application is not associated with Partner Center or lacks the Manager role, the secret value (not its id) is wrong or expired, or the tenant id belongs to another directory. |
| `MsSecretExpiryInvalid` when adding a Microsoft connection | The expiry date is in the past or more than 24 months away. |
| Verify fails for winget | The token was revoked or expired, a classic token lacks the `public_repo` scope, the fork name is wrong, the repository is not a fork of microsoft/winget-pkgs, or the token cannot push to it. |
| A winget plan fails with "refused the pull request" | A fine-grained token: open the pull request from the compare page in the error, or replace the token with a classic one. The next sync finds the pull request. |
| A winget plan fails with a SHA256 mismatch | The file at the installer address changed after you took its hash; leave the hash empty to let storeasy compute it, or fix the address. |
| `EmailNotVerified` when adding a connection | Verify your e-mail address first. |
