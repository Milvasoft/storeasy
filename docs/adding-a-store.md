# Adding a store or distribution channel

storeasy started with three stores (App Store Connect, Google Play, Microsoft Store) wired in directly. Every channel
after them (winget first, then Chrome Web Store, Microsoft Edge Add-ons, Firefox Add-ons, Flathub, Snap Store,
Homebrew; [urun-plani.md](urun-plani.md) section 7.1) plugs into the shared code through the extension points below,
so shared code has no branch for it. winget is the reference: follow its files
(`storeasy-api/src/Storeasy.Infrastructure/Stores/Winget/`, `storeasy-app/src/features/winget/`). The Chrome Web Store
(`Stores/Chrome/`, `features/chrome/`) is the reference for a store API with reviews, metrics and package intake.

One channel is one pull request. "Like the existing stores" means the same depth wherever the channel has the matching
concept; where it does not (no reviews, no store page, no metrics), leave it out and say so in
[connecting-stores.md](connecting-stores.md).

## 1. Identity

| Where | What |
|---|---|
| `Storeasy.Domain/Enums/StoreKind.cs` | A new value (next number) with a `[Description]`. |
| `Storeasy.Domain/Stores/StoreChannels.cs` | One row: key (`?store=` value and rules catalog key), display name, logo icon key, credential expiry (none, optional, required), reviews, metrics, console address. Shared code asks this table: connection icons, rules catalog keys, AI prompt store names, assisted step links, credential expiry on connections, which records get a metrics sync. |
| `Storeasy.Domain/Stores/StoreConnectionIcons.cs` | A constant for the logo key. |
| `storeasy-app/src/lib/stores/channels.ts` | The same row for the web app (key, text code, logo, credential expiry, reviews, metrics). `channels.test.ts` compares it with the API's table. Slugs, filters ("All stores" chips), store counts, the activity lanes and the connection icon map come from it. |
| `storeasy-app/src/lib/api/types.ts` | The `StoreKind` value. TypeScript then points at every `Record<StoreKind, ...>` that needs the new store. |
| `StoreIcons.tsx` | The logo mark (Simple Icons when the brand allows it, otherwise storeasy's own drawing), added to `MARKS`. |
| i18n | `enums.store.<key>`, `connections.stores.<key>` (title, body, time, needs), `connections.iconNames.<logo>`, and the resx `StoreKind.<Value>`. |

## 2. The engine: `IStoreChannel`

`Storeasy.Application/Interfaces/IStoreChannel.cs`, registered as a singleton. The store gateway (`StoreGateway`)
hands every call for the channel's store to it, so connections (add, verify, replace key), "sync apps" and the app sync
work without a branch:

| Member | Used by |
|---|---|
| `Inspect(secretJson)` | Adding and replacing a connection: offline checks, the fingerprint (no secret), the normalized secret to encrypt. Keep it free of I/O; the integration tests' gateway calls it directly. |
| `VerifyAsync` | **Verify** and key replacement: the cheapest read that proves the credential (winget: `GET /user` and the fork). |
| `ListAppsAsync`, `GetAppAsync`, `ListBuildsAsync`, `GetIconUrlAsync` | The generic store reads. A channel that cannot list a publisher's products throws a `StoreCallException` that says how to add one. |
| `SyncJob(organizationId, connectionId)` | `apps/sync` queues this job for the channel's connections. |
| `SameAccount(current, next)`, `CredentialLabel` | Replacing a key keeps the connection on the same account; the audit text names the credential without the secret. |

`ConnectionFingerprint` has generic fields for channels that write through a code host (`AccountLogin`, `Repository`,
`TokenKind`); add a field only when no existing one fits.

## 3. Reads, plans and the write

| Extension point | What it does | winget |
|---|---|---|
| `IStoreContentGateway` (scoped, `Store` = the channel) | The shared content read: listing values as `Listing|locale|Field`, versions, the raw data the planner needs. `ApplyAsync` may refuse and point at the channel's plan kind. | `WingetContentGateway` |
| `IChangePlanExtension` + a `ChangePlanKind` value | Plans of the channel: `ReadBaseAsync` reads the base again at approval (conflict check over the plan's `BaseKeys`), `ApplyAsync` performs the approved write in the apply job, `AuditActionOf` names its audit action. The plan's `CustomChanges` carries the exact payload. | `WingetPlanExtension` (opens the pull request) |
| `IReadinessContributor` | Store specific readiness items; the shared evaluator only adds the catalog's assisted steps and deadlines for a channel. | `WingetReadiness` |
| `IStoreJobHandler` + a `StoreJobKind` value | The channel's sync job (and any other job). | `WingetSyncJobHandler` |
| A cron job class + `ScheduledStoreSyncs` method | Periodic status reads, for example while something is in review. | `WingetSyncJob`, every 15 minutes for open pull requests |
| `INotificationDispatcher` | State changes notify like store version states: kind `VersionState`, webhook `version.state_changed`, activity `version.state`. | pull request state changes |

Three more extension points, added with the Chrome Web Store (`Storeasy.Infrastructure/Stores/Chrome/`,
`storeasy-app/src/features/chrome/`), plug a channel into shared features without a branch:

| Extension point | What it does | Chrome Web Store |
|---|---|---|
| `IStoreChannelReviews` (singleton) | The shared review sync asks it for the channel's reviews before the built in review gateway. Set `HasReviews` in both channel tables. Replies stay with the stores whose `ReviewRules.ReplyMaxLength` is above zero; a channel without a reply API leaves it at zero, the reply call answers `ReviewReplyNotSupported` and the reviews page says to answer in the store's console. | `ChromeChannel` (`fetchReviews`) |
| `IStoreChannelMetrics` (singleton) | The shared metrics sync asks it for daily rows of a channel's product (`StoreMetricAccumulator` builds the country and total slices); `BackfillDays` is the first read. Set `HasMetrics` in both channel tables. | `ChromeChannel` (`queryMetrics`) |
| `IArtifactChannel` (scoped) | Artifact intake hands a checked package of the channel's store to it: `Match` compares the package with the store record, `DeliverAsync` uploads it (never submits). Add the file extension and platform in `IntakeRules` (`Classify`, `StoreOf`), the package reader and the format's checks in `ArtifactChecks`, and the platform in the web intake labels. | `ChromeArtifactChannel` (`.zip`, platform `Extension`) |

Browser extension channels share one intake platform (`ArtifactPlatform.Extension`), which `IntakeRules.StoreOf` maps to
the Chrome Web Store today. Microsoft Edge Add-ons and Firefox Add-ons take the same ZIP: when they come, delivery should
pick the channel by the app's store records (or the session's `delivery.store`) instead of one store per platform.

Register everything in one `Add<Channel>Services` extension (see `WingetServiceCollectionExtensions`) called from
`InfraServiceCollectionExtensions`, with an options class bound from `Storeasy:<Channel>` whose base addresses the
tests replace. Use your own named `HttpClient`s; downloads from user given addresses go through `DownloadGuard` and
follow redirects by hand.

Where the only write path is a pull request to a public repository (winget-pkgs, Flathub, Homebrew), the plan prepares
the files and opening the pull request is the approved write; the pull request's state is the review state, and a merge
is the publication.

Reviews and replies plug in the same way. A channel whose API returns user reviews registers `IStoreChannelReviews`
(singleton; the shared review sync asks it before the built in review gateway, and `HasReviews` in both channel tables
adds the store to the review schedule and the reviews page filter). A channel whose API can answer them also registers
`IStoreChannelReplies` (singleton; the shared reply job asks it before the gateway) and sets its limit in
`ReviewRules.ReplyMaxLength`. Firefox Add-ons (`FirefoxChannel`) implements both.

Artifact intake picks the store from the file: `IntakeRules.StoreOf(platform, fileName)` sends `.xpi` (platform
`Extension`, kind `Xpi`) to Firefox Add-ons; the package reader and `ArtifactChecks` read and check the format, and intake
matches the package's identifier (the add-on id) with the store record's bundle id. The web mirrors it in the intake
labels (`storeOf(platform, fileName)`).

## 4. Numbers

Enum values must not collide; take the next free block and keep a channel's values together:

| Enum | Taken by the first stores | winget | Chrome Web Store | Next channels |
|---|---|---|---|---|
| `AuditAction` | ..., 150-155 (Microsoft), 300+ | 160-164 | 170-179 | 180-189, 190-199, ... (one block of ten each) |
| `StoreJobKind` | 1, 2, 30, 50-53, 60, 70, 80, 90, 110, 120-121, 400-407 | 130 | 140 | 150, 160, ... |
| `ChangePlanKind` | 1-17, 50, 70-72, 80 | 100 | 110 | 120, 130, ... |
| `StoreKind` | 1-3 | 4 | 5 | 6, 7, ... |
| `ArtifactPlatform`, `PackageKind` | 1-3, 1-4 | | 4 (`Extension`), 5 (`ExtensionZip`) | reuse `Extension` for browser extensions |

Firefox Add-ons took `StoreKind` 6, `AuditAction` 180-189, `StoreJobKind` 150, `ChangePlanKind` 120, `ArtifactPlatform` 4
(`Extension`, shared by browser extension channels) and `PackageKind` 6 (`Xpi`).

The package registries module ([package-registries.md](package-registries.md)) is not a store and takes no `StoreKind`;
its values sit in their own band: `AuditAction` 500-510, `StoreJobKind` 500, `NotificationType` 54, `NotificationEntity`
52.

Every enum value needs `"{Enum}.{Value}"` in both resx files (`ResxTests`), every audit action a web label
(`auditLabels.test.ts` reads the C# enum), every job kind a label in `lib/jobs/labels.ts`, every plan kind one in
`features/common/labels.ts`.

## 5. Data

Entities under `Storeasy.Domain/<Channel>/`, a `TableNames` constant, a `DbSet` and one migration
(`dotnet ef migrations add Add<Channel>Channel --project src/Storeasy.Api`). Each `[Table]` entity needs a
`Global.<Entity>` resx entry.

## 6. API and MCP

A controller (`<Channel>Controller`) with the channel's routes and rate limits, commands and queries under
`Features/<Channel>/` with validators, and the same operations in `McpToolCatalog.cs` (`<channel>_*` tools; writes are
`Plan<...>` tools so they are dry runs). Map new conflict keys in `ResponseStatusCodes`.

## 7. Rules catalog

A store file `rules/<key>.json`, listed in `rules/catalog.json`; add the key to the `store` enum and the `ruleId` pattern
of `rules/schema/catalog.schema.json`, bump `catalogVersion` everywhere and run `node rules/validate.mjs`. Rules with
`check.kind: assisted` become assisted steps linking to the channel's console address.

## 8. Web

A feature folder (`features/<channel>/`): `types.ts`, `api.ts` (and a contract test with handlers in
`test/support/<channel>.ts`), `keys.ts`, `rules.ts`, `i18n/en.ts` and `tr.ts`, the pages and `routes.tsx` added to
`App.tsx`, a workspace section in `AppWorkspace.tsx` (`SECTIONS`, `SECTION_PATHS`, `SECTION_STORES`), the credential form
in `AddConnectionDialog` and `ReplaceKeyDialog`, the fingerprint rows in `ConnectionsPage`, the "add product" dialog on
the apps page when products are added by id, and `storeLinks.ts`. Dictionary keyed responses lose their key case
(`pascalizeKeys`): send lists, not maps keyed by locale.

## 9. Tests

- Unit: parsing, mapping, planning and the channel's rules (`WingetTests`).
- Integration: a fake of the channel's service in `TestBase/Fakes/` wired in `StoreasyApiFactory` (the fake store
  gateway hands channel calls to the real engine), end to end from connection to the approved write and the review
  state (`WingetControllerTests`). No real network.
- Web: rules, contract, pages, connection form.

## 10. Docs

[connecting-stores.md](connecting-stores.md) (credential, what is supported and what is not), [architecture.md](architecture.md)
(store engines), [api.md](api.md), [mcp.md](mcp.md), [self-host.md](self-host.md) (settings and schedules) and
[urun-plani.md](urun-plani.md) section 7.1.
