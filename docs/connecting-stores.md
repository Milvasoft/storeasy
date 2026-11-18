# Connecting stores

storeasy talks to the stores only through their official APIs, with credentials your organization adds under
**Organization > Connections**. Adding, verifying and deleting a connection needs the Owner or Admin role and a verified
e-mail address. An organization can have several connections per store (for example two Apple developer accounts).

## What storeasy keeps

| Kept | Never |
|---|---|
| The credential, sealed with AES-256-GCM envelope encryption (a data key per connection, wrapped by the instance master key) | Returned by any endpoint, MCP tool, export or log |
| A fingerprint shown in the app: App Store Connect key id and the last 4 characters of the issuer id; Google service account e-mail and project id | Your Apple ID, Google account or Partner Center password |
| Last verification time and result | Copies of the credential in jobs, plans or audit entries |

Only the store job runner and the verify command open a credential, in memory, for the duration of the call. To replace
a credential, delete the connection and add it again. See [self-host.md](self-host.md#7-master-key-backup-and-rotation)
for the master key.

## App Store Connect

You need an App Store Connect API **team key**.

1. In App Store Connect, open **Users and Access > Integrations > App Store Connect API**. The Account Holder has to
   request API access once.
2. Under **Team Keys**, generate a key. Role: **App Manager**. It covers what storeasy does: app information and
   version texts, screenshots and previews, builds and export compliance, TestFlight, pricing and availability, age
   rating, review information, submission and customer review replies. **Admin** is not needed; **Developer** is not
   enough for metadata.
3. Download the `.p8` file (Apple lets you download it once) and note the **Key ID** and the **Issuer ID** shown on the
   page.
4. In storeasy, add an Apple connection with the issuer id, key id and the content of the `.p8` file, then verify it.
5. Run **Sync** to list the account's apps. Apps with the same bundle id in another store are paired automatically.

Apple does not let the API create apps: create the app record in App Store Connect first.

## Google Play

You need a Google Cloud service account with a JSON key, invited to your Play Console account.

1. In Google Cloud, pick or create a project, enable the **Google Play Android Developer API**, create a service
   account and create a **JSON key** for it. If your organization policy blocks key creation
   (`iam.disableServiceAccountKeyCreation`), allow it for this project.
2. In Play Console, open **Users and permissions > Invite new users**, enter the service account e-mail and grant, per
   app or for the account:
   - view app information;
   - edit the store listing, pricing and distribution;
   - manage testing tracks and edit tester lists, and release to testing tracks;
   - release to production (only if storeasy should release to production; storeasy still requires the Admin or Owner
     role and a typed confirmation);
   - reply to reviews.

   Play Console labels change over time; pick the closest match.
3. In storeasy, add a Google connection with the content of the JSON key file, then verify it.
4. Play has no API to list apps: add each app by its package name. storeasy proves access by opening and discarding an
   edit, and removes a record Play refuses.

Create the app in Play Console first. Google can require the first bundle of a new app to be uploaded in Play Console.

## Microsoft Store

> Planned (plan phase 5). Adding a Microsoft connection answers "not supported yet" today.

You will need a Microsoft Entra application associated with Partner Center:

1. In Partner Center, open **Account settings > User management > Microsoft Entra applications** and add (or create)
   an application with the **Manager** role.
2. Create a client secret for it. Secrets live at most 24 months and Microsoft recommends 12; storeasy will keep the
   expiry date and warn 30 days ahead.
3. Note the **tenant ID**, **client ID**, **client secret** and your **seller ID** (Partner Center account settings).
4. In storeasy, add a Microsoft connection with these four values.

Microsoft's APIs can only change an app after its first submission in Partner Center, and a submission edited by hand
in Partner Center can no longer be changed through the API. storeasy shows both as warnings.

## Troubleshooting

| Symptom | Check |
|---|---|
| Verify fails with 401 for Apple | Key revoked, wrong issuer id, or the `.p8` content was changed (keep the `BEGIN` and `END` lines). |
| Verify fails with 403 for Google | The service account was not invited in Play Console, or the API is not enabled in its Cloud project. |
| A Play app disappears after sync | Play refused the service account for that package: grant it access to the app. |
| `EmailNotVerified` when adding a connection | Verify your e-mail address first. |
