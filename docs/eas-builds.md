# EAS builds

storeasy can start Expo EAS builds of your apps through the Expo API, follow them, and hand the finished package to
the [build intake](ci-intake.md) or submit it with EAS Submit. A build starts only when a member asks for it (in the
web app or over MCP) and types the confirmation; nothing starts on a schedule.

An instance admin can switch the whole feature off with the `EasBuilds` feature flag (the EAS tab and the Expo section
of the connections page disappear, the endpoints and MCP tools answer 503 and the schedules stop).

## Connect Expo

On **Connections**, the **Expo (EAS builds)** section takes one Expo access token per organization (Manage connections
permission). Use a robot token: [connecting-stores.md](connecting-stores.md#expo-eas-builds). storeasy asks Expo whose
token it is (`meActor`) and refuses a token Expo does not accept. The token is sealed with the same envelope encryption
as store credentials and never shown again; the section shows only the robot or user it belongs to. The token is never
handled by MCP.

## Link an app

The **EAS** tab of an app lists the EAS projects the token can see. The project whose latest build has the bundle id or
package name of the app's store records comes first, then a project whose slug or name matches the app's name. Linking
stores the project id, account and slug (Edit content permission) and reads the project's recent builds.

## Start a build

EAS builds from the GitHub repository connected to the project on expo.dev (**Project settings > GitHub**), so nothing
is uploaded from storeasy. Without that connection storeasy cannot start builds and the tab links to the project's
GitHub settings on expo.dev.

**Start build** (Release to test tracks permission) asks for:

- the platform: iOS, Android or both (one build each);
- the build profile: read from `eas.json` at the root of the repository when the organization has a GitHub token
  (Package registries credentials) that can read it, otherwise `development`, `preview` and `production` are offered and
  any name can be typed;
- the git ref: a branch, tag or commit;
- what happens after the build (see below);
- the confirmation: the app name and the platform word, for example `Pusula iOS`, `Pusula Android` or `Pusula all`.

The dialog says once that a build uses your EAS build quota. storeasy calls `githubApp.createGitHubBuild`; Expo answers
with a background job receipt, and the build appears in the list as **Requested** until Expo created it.

## Follow builds

Every two minutes storeasy reads the builds still running (and the receipts of requested builds) from Expo: state
(requested, new, in queue, in progress, canceling, finished, errored, canceled, not started), platform, profile, app
version and build number, commit, the build's page on expo.dev, logs, the package address, dates and the error. A
build that finished, failed or was canceled is audited and notified. Builds started elsewhere (eas-cli, GitHub
triggers) are read when the app is linked, with **Read builds**, and every hour, and shown as started outside storeasy.

A queued or running build can be canceled (`build.cancelBuild`, Release to test tracks).

## After the build

When starting a build, choose what happens once it finished; each app keeps defaults under **Defaults of this app**:

| Choice | What storeasy does |
|---|---|
| Nothing | Nothing; the package can still be sent or submitted by hand. |
| Send to intake | Downloads the package (IPA, AAB, APK; only from `expo.dev`, `*.expo.dev` and `storage.googleapis.com`) into the build intake; the app's delivery rule takes it to TestFlight or a Play test track as for any upload. |
| EAS Submit | Submits the build with EAS Submit: iOS to App Store Connect (TestFlight), Android to the chosen Play track and release status. |

**Prepare the review or production plan** (optional): once the build reached its test channel (on the App Store a
processed build with its build number, on Play the delivered intake package or the finished submission), storeasy
prepares the plans and leaves them pending: on the App Store attaching the build to the editable version (with the
export compliance answer of an earlier build of the app when the build has none) and the review submission; on Play the
production release of its version code. Owners and admins are notified with a link to the plan. **Nothing is approved
automatically**: the plans keep their own permission (Submit for review, Release to production) and typed confirmation.
On the App Store, approve the build plan before the review submission. If the build does not reach its test channel
within 24 hours, the step stops with a note on the build.

## Hand off by hand

On a finished build:

- **Send to intake** hands the package to the build intake (Release to test tracks).
- **EAS Submit** submits it (Release to test tracks; the Play production track also needs Release to production) after
  the typed confirmation. EAS Submit uses the credentials stored on expo.dev: for iOS the App Store Connect API key
  for submissions of the project (`eas credentials`) and the Apple app id of the app's App Store record in storeasy;
  for Android the Google service account key for submissions and the package name of the app's Google Play record.
  storeasy never sends its own store keys to Expo. Without those credentials the dialog links to the project's
  credentials on expo.dev.

Submissions are followed like builds (waiting for the build, in queue, in progress, finished, errored, canceled),
audited and notified, and listed under **EAS Submit**.

## Permissions

| Action | Permission |
|---|---|
| See the EAS tab, builds, submissions and profiles | Any member |
| Link or unlink a project, save the defaults, read builds | Edit content |
| Start or cancel a build, send to intake, EAS Submit | Release to test tracks (Play production track: Release to production) |
| Add or remove the Expo token | Manage connections |

## MCP

`eas_projects_list`, `eas_builds_list`, `eas_build_get`, `eas_build_profiles`, `eas_submissions_list` and
`eas_submission_get` read; `eas_project_link`, `eas_chain_save` and `eas_builds_sync` write storeasy data;
`eas_build_start` and `eas_submit_start` need `confirm: true` and the typed `confirmText`, and return the storeasy ids
to follow with `eas_build_get` and `eas_submission_get`; `eas_build_cancel` and `eas_build_send_to_intake` act on a
build. Permissions and audit are the same as in the web app ([mcp.md](mcp.md)).
