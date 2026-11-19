# Connecting stores

storeasy talks to the stores only through their official APIs, with credentials your organization adds under
**Organization > Connections**. Adding, verifying and deleting a connection needs the **Manage store connections**
permission (Owner and Admin, or a custom role that has it) and a verified e-mail address. An organization can have
several connections per store (for example two Apple developer accounts).

Each connection can have an optional icon, picked when it is added and changed later with **Edit** (with its name): the
store's own logo or a colored generic icon. Records that come from that connection show the icon in place of the
`AS` / `GP` / `MS` code; places that know only the store (filters, counts, store columns) use the icon of the store's
oldest connection that has one. Without an icon the code stays. Store logos are trademarks of their owners and only
identify the store; each is offered only for its own store.

## What storeasy keeps

| Kept | Never |
|---|---|
| The credential, sealed with AES-256-GCM envelope encryption (a data key per connection, wrapped by the instance master key) | Returned by any endpoint, MCP tool, export or log |
| A fingerprint shown in the app: App Store Connect key id and the last 4 characters of the issuer id; Google service account e-mail and project id; Microsoft seller id, the last 4 characters of the tenant and client ids and the secret's expiry date | Your Apple ID, Google account or Partner Center password |
| Last verification time and result | Copies of the credential in jobs, plans or audit entries |

Only the store job runner and the verify command open a credential, in memory, for the duration of the call. To replace
an Apple or Google credential, delete the connection and add it again; a Microsoft client secret is replaced in place
(see below). See [self-host.md](self-host.md#7-master-key-backup-and-rotation)
for the master key.

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

## Troubleshooting

| Symptom | Check |
|---|---|
| Verify fails with 401 for Apple | Key revoked, wrong issuer id, or the `.p8` content was changed (keep the `BEGIN` and `END` lines). |
| Verify fails with 403 for Google | The service account was not invited in Play Console, or the API is not enabled in its Cloud project. |
| A Play app disappears after sync | Play refused the service account for that package: grant it access to the app. |
| Verify fails for Microsoft | The application is not associated with Partner Center or lacks the Manager role, the secret value (not its id) is wrong or expired, or the tenant id belongs to another directory. |
| `MsSecretExpiryInvalid` when adding a Microsoft connection | The expiry date is in the past or more than 24 months away. |
| `EmailNotVerified` when adding a connection | Verify your e-mail address first. |
