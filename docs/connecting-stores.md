# Connecting stores

storeasy talks to the stores only through their official APIs, with credentials your organization adds under
**Organization > Connections**. Adding, verifying and deleting a connection needs the **Manage store connections**
permission (Owner and Admin, or a custom role that has it) and a verified e-mail address. An organization can have
several connections per store (for example two Apple developer accounts).

Each connection can have an optional icon, picked when it is added and changed later with **Edit** (with its name): the
store's own logo or a colored generic icon. Records that come from that connection show the icon in place of the
`AS` / `GP` / `MS` / `WG` / `FX` code; places that know only the store (filters, counts, store columns) use the icon of the store's
oldest connection that has one. Without an icon the code stays. Store logos are trademarks of their owners and only
identify the store; each is offered only for its own store.

## What storeasy keeps

| Kept | Never |
|---|---|
| The credential, sealed with AES-256-GCM envelope encryption (a data key per connection, wrapped by the instance master key) | Returned by any endpoint, MCP tool, export or log |
| A fingerprint shown in the app: App Store Connect key id and the last 4 characters of the issuer id; Google service account e-mail and project id; Microsoft seller id, the last 4 characters of the tenant and client ids and the secret's expiry date; winget fork, its owner and the token kind (and the expiry you entered); Firefox Add-ons JWT issuer and AMO user id | Your Apple ID, Google account, Partner Center, GitHub or Firefox account password |
| Last verification time and result | Copies of the credential in jobs, plans or audit entries |

Only the store job runner, the verify command and replacing a key open a credential, in memory, for the duration of the
call. See [self-host.md](self-host.md#7-master-key-backup-and-rotation) for the master key.

## Replacing a key

An Apple, Google, winget or Firefox Add-ons key is replaced in place with **Replace key** on the connection's card (`PUT
storeconnections/connection/key`, Manage store connections permission and a verified e-mail address). The connection
keeps its id, so its apps, store records, jobs, plans and history stay. Typical case: the connection uses an App Manager
key and the App Store analytics reports need an Admin key; Apple does not change a key's role, so create a new Admin key
and replace the old one with it.

- **Apple:** the new key must belong to the same App Store Connect team: storeasy compares its issuer id with the one
  sealed in the connection and refuses another team's key (`StoreKeyIssuerMismatch`). The key id may change.
- **Google:** any service account JSON key is taken; it may be another service account or another Cloud project.
- **winget:** a new GitHub token for the same fork; another fork is refused (`StoreKeyAccountMismatch`). Enter the new
  token's expiry date with it.
- **Firefox Add-ons:** a new AMO API key of the same account (the user id in the JWT issuer); another account is refused
  (`StoreKeyAccountMismatch`).

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

## Chrome Web Store

storeasy talks to the [Chrome Web Store API v2](https://developer.chrome.com/docs/webstore/api) (API v1.1 was
supported until 15 October 2026). The API works on items that already exist: it cannot create an item or list a
publisher's items, so the first version is uploaded in the
[developer dashboard](https://chrome.google.com/webstore/devconsole) and the item is then added to storeasy by its
extension id.

1. In the Google Cloud console, enable the **Chrome Web Store API** in a project.
2. Pick one credential:
   - **Service account** (recommended): create a service account and a JSON key for it (no Cloud roles are needed), then
     in the developer dashboard add the service account's email under **Account** (one service account per publisher).
   - **OAuth client and refresh token**: configure the OAuth consent screen, create an OAuth client id of type **Web
     application**, and make a refresh token once (for example in the OAuth Playground with your own client) for the
     scope `https://www.googleapis.com/auth/chromewebstore`, signed in with the Google account that owns the items.
3. Copy the **publisher id** from **Account** in the developer dashboard (switch to the right publisher first).
4. In storeasy, add a **Chrome Web Store** connection: the publisher id, the service account JSON key or the OAuth client
   id, client secret and refresh token, and optionally one extension id of the publisher. The secret is
   `{"publisherId": "...", "serviceAccountKey": "{...}", "itemId": "..."}` or
   `{"publisherId": "...", "clientId": "...", "clientSecret": "...", "refreshToken": "...", "itemId": "..."}`.
5. Verify it. storeasy gets an access token for the Chrome Web Store scope and, when an extension id was given, reads
   that item's status under the publisher (the API has no call that reads a publisher, so without an id only the token
   is proven).
6. Add each item by its extension id (**Apps > Add a Chrome Web Store item** or `chrome/item`), to an existing app or a
   new one. The API returns no item name, so the app keeps the name you give.

The fingerprint is the publisher id, the credential kind and the service account email or the end of the OAuth client
id; the secret is sealed like every store secret and never shown. **Replace key** takes a new credential for the same
publisher (another publisher is refused with `StoreKeyAccountMismatch`) and checks it with Google first. Neither
credential expires on its own, so no expiry date is kept.

**What storeasy reads.** `fetchStatus` of each item: the published revision (state, version, deploy percentage), the
revision submitted since the last publish (pending review, staged, rejected, cancelled), the last upload's state and the
policy flags (warned, taken down); the rating summary; the seven day users. The versions become builds. Reviews come
through the shared review sync (`fetchReviews`, newest first, up to 1,000) and store metrics through the shared metrics
sync (`queryMetrics`: installs as downloads, impressions and page views, by country; the first read goes back 90 days).

**Packages.** Artifact intake takes `.zip` extension packages (manifest.json at the root of the ZIP; the platform is
"Browser extension"). The package is read and checked before anything reaches the store: Manifest V3 only (the store
removed the last Manifest V2 items on 31 August 2026), a valid version higher than every version the store and intake
know, the store's 75 character name and 132 character summary, a default locale for `__MSG_` names, and warnings for a
missing 128 pixel icon, a Manifest V2 background page, an `update_url` outside the store and broad host access. With a
delivery rule for browser extensions (target "upload only"), intake uploads the package to the item and stops: the
upload creates the draft in the developer dashboard, nothing is submitted. Without a rule the package is held. CRX
packages are not taken (the store signs uploaded ZIPs itself).

**What storeasy writes, always as a change plan** (`chrome/change`, dry run, approved under **Plans** with **Submit for
review** and the extension id and version typed):

- **Submit**: upload an intake package (its SHA-256 checked again, the status read until the store has processed it)
  or take the package uploaded last, then `publish`: for review, staged after approval (`STAGED_PUBLISH`), or asking to
  skip the review where the item qualifies; optionally with an initial deploy percentage. Refused while a submission is
  in review.
- **Publish staged**: publish an approved, staged revision.
- **Deploy percentage**: raise the published revision's rollout (only up, no new review; the store allows partial rollout
  for items with more than 10,000 seven day users).
- **Cancel submission** of a revision in review or staged.
- **Roll back**: publish an earlier package again under a new, higher version number, with a reason the store keeps.

**Review state.** A submission is *In review*, then *Published* (or *Published to testers*), *Staged* or *Rejected*;
storeasy reads items with a submission in review or an upload in progress every 15 minutes (`chrome/sync` reads one item
at once). A state change and a policy warning or take down are audited and notified like a store version state
(in-app, e-mail, web push, the `version.state_changed` webhook). The rejection reason is shown only in the developer
dashboard; the API does not return it.

**Not in the API, so assisted steps.** The store listing (detailed description, category, language, screenshots, promo
tiles), the **Privacy practices** tab (single purpose, permission justifications, data use disclosure, privacy policy,
Limited Use certification) and the visibility (public, unlisted, private, trusted testers: API v2 no longer changes it)
are edited in the developer dashboard; the rules catalog turns them into assisted steps with the dashboard's address.
Review replies are not in the API either: reviews are read in storeasy and answered in the dashboard.
## Firefox Add-ons

Firefox Add-ons (addons.mozilla.org, AMO) is reached through the **AMO API v5** with an **API key** of your AMO
account. Every request is signed with a short lived JWT (HS256) made from the key; the key acts as the account that
created it, so that account must be an author of the add-ons.

1. Sign in to [addons.mozilla.org](https://addons.mozilla.org) with the Firefox account that authors your add-ons.
2. Open the **Developer Hub > Tools > Manage API Keys**
   ([addons.mozilla.org/developers/addon/api/key](https://addons.mozilla.org/developers/addon/api/key/)) and generate
   credentials: a **JWT issuer** (`user:12345:67`) and a **JWT secret**.
3. In storeasy, add a **Firefox Add-ons** connection with both values (the web app has a form; the secret is
   `{"issuer": "user:12345:67", "secret": "..."}`).
4. Verify it. storeasy reads `GET /api/v5/accounts/profile/` with a token that lives at most five minutes.
5. **Sync apps** (or the connection's sync) lists the add-ons the account authors (`GET /api/v5/addons/addon/`) and adds a
   storeasy app for each new one. **Apps > Add a Firefox add-on** (`firefox/addon`) adds one by its numeric id, guid or
   slug, for example to attach it to the app of the same program in another store.

The fingerprint is the JWT issuer and the AMO user id it names; the secret is sealed like every store secret and never
shown. **Replace key** takes a new key of the same AMO account (another account is refused with
`StoreKeyAccountMismatch`) and checks it with AMO first. Revoke the old key in the Developer Hub afterwards.

**What storeasy reads.** The add-on (status, guid, slug, default locale, name, summary, description, homepage, support
site and e-mail in every locale, categories, previews, icon, rating, average daily users, weekly downloads) and its
listed and unlisted versions with their file review status, release notes, license, source code flag and data collection
permissions. The store record keeps the numeric AMO id as its external id and the guid as its bundle id. Ratings are read
into **Reviews** by the shared review sync (every 30 minutes).

**What storeasy writes, always as a change plan** (`firefox/change`, dry run, approved under **Plans**):

- **Listing**: name, summary, description, homepage, support site and e-mail per locale (an empty value removes that
  locale's value; the default locale keeps its name and summary), and the categories (`PATCH /addons/addon/{id}/`).
- **New version**: an `.xpi` intake received (upload it with the web, the CLI or CI like any package). Approving the plan
  checks the stored file's SHA-256, uploads it on the **listed** or **unlisted** channel (`POST /addons/upload/`), waits
  for AMO's validation (a failure stops the plan with AMO's messages), then creates the version with its release notes,
  notes to the reviewer, license and the source code archive (`POST /addons/addon/{id}/versions/`, source code as a
  multipart `PATCH`). The approval needs **Submit for review** and the guid and version typed as confirmation. Listed
  versions wait for Mozilla's review; unlisted versions are signed at once for self distribution.
- **Previews**: add PNG or JPEG assets, remove previews (`/addons/addon/{id}/previews/`).
- **Icon**: a square PNG or JPEG asset (`PATCH /addons/addon/{id}/` with `icon`).
- **Release notes** of an existing version (`PATCH /addons/addon/{id}/versions/{id}/`).

Before a plan is saved storeasy runs the package and policy checks it can run offline: `manifest.json` at the root of the
archive, Manifest V2 or V3 (V2 is a warning), a version of one to four numbers higher than every listed and unlisted
version and not used before, `browser_specific_settings.gecko.id` present (required for Manifest V3) and equal to the
add-on's guid, the data collection declaration (`data_collection_permissions`, required for new add-ons since 3 November
2025; a warning here), name and summary limits, a license for the first listed version, broad host access and an
`update_url` (warnings), and scripts that look minified or bundled (a warning that AMO requires the source code and build
instructions; attach the source asset and write the instructions into the notes to the reviewer). Intake runs the same
manifest checks when the package arrives and holds it for a plan (there is no automatic delivery to AMO).

**Review state.** The newest version's file status (*Awaiting review*, *Approved*, *Disabled*) and the add-on status
(*Approved*, *Awaiting review*, *Incomplete*, *Disabled by Mozilla*) are read by the sync; add-ons with a version awaiting
review are read every 15 minutes. A change is audited and notified like a store version state (in-app, e-mail, web push,
the `version.state_changed` webhook). A rejected version shows the reviewer's reason in the Developer Hub.

**Ratings and replies.** Ratings come in through the shared review sync; a reply is sent as the developer
(`POST /ratings/rating/{id}/reply/`, answering again edits the reply). AMO publishes no reply limit; storeasy keeps
replies to 3,000 characters.

**Not in the AMO API.** There is no statistics API: daily users and weekly downloads come from the add-on detail, the
charts stay in the Developer Hub, so Firefox has no **Store page performance** section. The privacy policy, the EULA,
the support details outside the fields above, the add-on's contributions link and disabling or deleting the add-on are
left to the Developer Hub (rules catalog assisted steps cover the policy and privacy policy). Themes cannot upload
previews. A `.zip` intake package is a Chrome Web Store package; upload a Firefox package as `.xpi`.

## Store page performance

The app's **Store performance** tab shows impressions, page views, downloads and the conversion rate (downloads divided
by page views) per day, store, country and traffic source. storeasy reads them once a day (05:30 UTC) and on **Fetch
now**; reads never change anything in a store. The first read goes back as far as each source allows, later reads repeat
the last 7 days because the stores complete recent days late. Daily rows are kept for 25 months.

| Store | Source | What the owner sets up |
|---|---|---|
| App Store | Analytics Reports API: "App Store Discovery and Engagement" (impressions, product page views) and "App Downloads" (first-time downloads, redownloads), standard reports, daily | Press **Enable analytics reports** once per app (Manage connections permission, a confirmation, audited). This is the one store write: it creates an ONGOING report request at Apple. Creating it needs an API key with the **Admin** role; reading the reports needs **Admin** or **Sales and Reports**. Apple delivers the first data one or two days later and every day after that with a delay of one to two days. Apple stops a request nobody reads for a long time; storeasy then shows it as not enabled again. |
| Google Play | Play Console statistics exported to Cloud Storage: `stats/store_performance/store_performance_<package>_<yyyyMM>_country.csv` and `..._traffic_source.csv` (store listing visitors, acquisitions) | Enter the reports bucket on the tab (Play Console, **Download reports**, **Copy Cloud Storage URI**, e.g. `gs://pubsite_prod_rev_01234567890987654321`). The service account must be a Play Console user with **View app information and download bulk reports** set for the whole account (global). storeasy reads it with the `devstorage.read_only` scope; a refusal shows as "No access" on the tab. The first read takes the last 6 monthly files. Play reports no impressions. |
| Chrome Web Store | Chrome Web Store API `queryMetrics`: installs, impressions and page views per day by country | Nothing new: the connection's credential reads them. The first read takes the last 90 days. The Chrome Web Store reports no traffic sources. |
| Microsoft Store | Partner Center analytics API: `analytics/appacquisitions` (daily acquisitions by market and store client) and `analytics/funnel` (page views, one call per day) | Nothing new: the existing Entra app is used. It needs a Partner Center role that can read analytics (Manager or Developer); a 401 or 403 shows as "No access". The first read takes the last 90 days. Microsoft reports no impressions and counts funnel page views as users. |

Conversion is computed only over the rows that report both page views and downloads, so one store's downloads are
never divided by another store's page views. The country filter and the source breakdown are separate slices of the
same data; Google Play reports countries and traffic sources in separate files, so a country filter with a source
breakdown shows only the stores that report both together.

## Package registries

Releases and registry operations of the [package registries module](package-registries.md) use one GitHub token and one
key per registry for the organization, added under **Package registries > Credentials for releases and actions**
(Manage connections permission). They are sealed like store credentials, shown only by a fingerprint and used only by
approved plans. Give each the least access:

- **GitHub:** a fine-grained personal access token (GitHub > Settings > Developer settings > Fine-grained tokens), owned
  by the organization that owns the repositories, limited to the repositories you release, with **Contents: Read and
  write** (version files, bump commits, tags), **Actions: Read and write** (workflow dispatch and runs) and **Metadata:
  Read** (always included). Add **Workflows: Write** only if a version file sits under `.github/workflows`. Set an
  expiration date. Note: whether a tag created with the token starts `on: push: tags` workflows has not been confirmed
  for every setup (tags created by the Actions `GITHUB_TOKEN` do not); a version bump commit or a workflow dispatch
  always starts the CI.
- **NuGet:** an API key (nuget.org > API Keys) with only the **Unlist package** scope and a glob pattern for your
  package ids. Enter its expiry date; storeasy reminds you before it.
- **npm:** a granular access token with **Read and write** on the packages (or the scope) you manage. Deprecation and
  dist-tags do not publish; if your account enforces two factor authentication for writes, the token needs the bypass
  option, otherwise npm refuses it.
- **crates.io:** an API token (crates.io > Account Settings > API Tokens) with the **yank** scope, limited to your crates.
- **RubyGems:** an API key with only the **Yank rubygem** scope, optionally limited to one gem. A key on an account
  with multi factor authentication for API calls is refused, since storeasy cannot send a one time code.

PyPI, Packagist and NuGet deprecation have no write API that storeasy uses; no key is stored for them.

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
