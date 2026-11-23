# Package registries

storeasy watches the packages you publish to npm, NuGet, PyPI, crates.io, Maven Central, RubyGems, Packagist and pub.dev: the latest version, when it was published,
which CI run published it, provenance, and alerts about deprecated or yanked versions and security advisories. It does
not publish anything: publishing stays in your CI (GitHub Actions, Azure DevOps, changesets, release-please), and
storeasy stores no registry token. Only the registries' public APIs are read.

The module is separate from the stores: a package is not a store record, it has no store page, screenshots or review.
An instance admin can switch the whole module off with the `Registries` feature flag (the menu item disappears, the
endpoints and MCP tools answer 503 and the schedule stops).

## Adding a package

**Package registries** in the menu, then **Add package**: pick the registry and type the name. Optionally link the
package to a storeasy app. storeasy checks the name against the registry's rules and asks the registry whether the
package exists, saves it with the registry's own spelling and reads it at once.

| Registry | Name | Example |
|---|---|---|
| npm | lower case, optionally scoped | `left-pad`, `@scope/name` |
| NuGet | package id | `Newtonsoft.Json` |
| PyPI | project name | `requests` |
| crates.io | crate name | `serde_json` |
| Maven Central | `groupId:artifactId` | `com.google.code.gson:gson` |
| RubyGems | gem name | `rake` |
| Packagist | `vendor/package`, lower case | `monolog/monolog` |
| pub.dev | package name, lower case | `http` |

Adding, removing and **Sync now** need the Edit content permission; every member can read.

## What storeasy reads

| Registry | Source | Versions | Provenance and CI run |
|---|---|---|---|
| npm | `registry.npmjs.org/<name>` | `versions`, `time`, `deprecated` per version, `dist-tags.latest` | `dist.attestations.provenance` per version; the SLSA provenance of the latest version (`/-/npm/v1/attestations/<name>@<version>`) names the source repository, the workflow and the run |
| NuGet | registration index (`registration5-gz-semver2`) | `version`, `published`, `listed`, `deprecation` | none: NuGet has no provenance, so storeasy shows "Not offered" and raises no provenance alert |
| PyPI | `pypi.org/pypi/<name>/json` | `releases` with upload time, `yanked` and `yanked_reason` | the Integrity API (`/integrity/<project>/<version>/<file>/provenance`) names the repository and the workflow; PyPI does not name the run. The latest version is checked on every sync, older ones a few per sync |
| crates.io | `crates.io/api/v1/crates/<name>` | `num`, `created_at`, `yanked`, `yank_message` | `trustpub_data` of trusted publishing (GitHub run or GitLab job) |
| Maven Central | `repo1.maven.org/maven2/<group path>/<artifact>/maven-metadata.xml`, dates from `search.maven.org` (`core=gav`) | `versioning/release`, `versions/version`, `timestamp` | none |
| RubyGems | `rubygems.org/api/v1/gems/<name>.json` and `/api/v1/versions/<name>.json` | `number`, `created_at` | sigstore attestations (`/api/v1/attestations/<name>-<version>.json`) of the latest version and a few unchecked ones per sync |
| Packagist | `repo.packagist.org/p2/<vendor>/<package>.json` (minified) | `version`, `time`, `abandoned` as the deprecation of the latest version | none (Packagist reads your VCS) |
| pub.dev | `pub.dev/api/packages/<name>` and `/options` | `version`, `published`, `retracted` (as yanked), `isDiscontinued` as the deprecation of the latest version | none |
| OSV.dev | `api.osv.dev/v1/querybatch` | | security advisories of the latest version (id, severity, summary) |

The latest version is the registry's own: npm `dist-tags.latest`, crates.io's highest stable version, PyPI's current
release and NuGet's highest listed stable version.

## Alerts

| Alert | Open while |
|---|---|
| Deprecated | the latest version is deprecated, or a version was deprecated after you started tracking the package |
| Yanked | a version was yanked (or unlisted on NuGet) after you started tracking the package |
| No provenance | the latest version was published without provenance (npm, PyPI, crates.io, RubyGems) |
| Publisher mismatch | the package has a trusted publishing setup and the latest version's provenance names another repository or workflow |
| Token expiring | a recorded token expires within 14 days or has expired (see token reminders) |
| Security advisory | OSV.dev reports an advisory for the latest version; when OSV.dev cannot be read the alert stays as it was |

An alert is resolved by the sync that sees its condition gone. A new version, a newly deprecated or yanked version and a
new advisory are written to the audit log and notified to the organization's editors (notification kind
"Package registries", in the app by default).

## Sync

Every hour storeasy queues a sync for the packages not read in the last six hours, so every package is read about every
six hours; **Sync now** reads one package (or all of them) at once. A package the registry cannot be read for keeps its
data and shows the error.

## Trusted publishing

The **Trusted publishing** section of a package page walks through moving publishing from a stored token to OIDC:

1. **On the registry**: the values to enter on the package's trusted publisher settings (owner, repository, workflow file,
   environment; namespace, project and CI file path for GitLab) with a link to that page.
2. **In your repository**: a ready to copy CI file that publishes on a `v*` tag with OIDC, parameterized by your values
   (GitHub Actions; GitLab CI/CD for npm and PyPI).
3. **Check**: after the next release storeasy compares the latest version's provenance with the saved setup. Every sync
   repeats the check and raises a publisher mismatch alert when a release comes from elsewhere.

| Registry | CI providers | CI file |
|---|---|---|
| npm | GitHub Actions, GitLab CI/CD | `npm publish --provenance` (npm 11.5.1 or later; GitLab `id_tokens` `NPM_ID_TOKEN` with `aud: "npm:registry.npmjs.org"`) |
| PyPI | GitHub Actions, GitLab CI/CD | `pypa/gh-action-pypi-publish@release/v1` with `attestations: true`; GitLab `PYPI_ID_TOKEN` (`aud: pypi`) and `twine upload` |
| crates.io | GitHub Actions | `rust-lang/crates-io-auth-action@v1` and `cargo publish` with its `token` output |
| NuGet | GitHub Actions | `NuGet/login@v1` (your nuget.org profile name as `user`) and `dotnet nuget push` with its `NUGET_API_KEY` output |
| RubyGems | GitHub Actions | `rubygems/release-gem@v1` |
| pub.dev | GitHub Actions | the reusable `dart-lang/setup-dart/.github/workflows/publish.yml@v1` on a version tag |
| Maven Central, Packagist | none | storeasy says so; keep the token and add a reminder |

NuGet and pub.dev do not say which workflow published a version, so the check reads "Cannot be checked" for them.
storeasy never writes to a registry or your repository.

## Token reminders

Still publishing with a long lived token? Under **Token reminders** record a name for it, the registry, where it is used
(for example the CI secret `NPM_TOKEN` in `acme/lib`) and its expiry date. storeasy warns 14 and 3 days before and on the
day (an alert, an audit entry and a notification), and suggests trusted publishing where the registry offers it. The
token itself is never accepted: a value that looks like one (`npm_...`, `pypi-...`, NuGet and crates.io keys, GitHub
and GitLab tokens, long secret like strings) is refused. The check runs daily and right after a reminder is saved.

## Downloads and usage

The **Downloads** tab of a package page and the downloads column of the summary (sortable) show what each registry
publishes. Every sync stores the registry's daily counts and a daily snapshot of the all time total, so a trend builds
up even where the registry gives totals only.

| Registry | Source | All time | Recent | Daily | Per version |
|---|---|---|---|---|---|
| npm | `api.npmjs.org/downloads/range` and `/versions/<name>/last-week` | none | last 30 days | yes (180 days) | last 7 days |
| NuGet | search API (`totalDownloads`, `versions[].downloads`) | yes | none | from snapshots | all time |
| PyPI | pypistats.org (`/recent`, `/overall?mirrors=false`) | none | last 30 days | yes (180 days) | none |
| crates.io | crate `downloads`, `recent_downloads`, `/downloads` | yes | last 90 days | yes | all time |
| RubyGems | gem `downloads`, versions `downloads_count` | yes | none | from snapshots | all time |
| Packagist | `packages/<name>.json` `downloads`, `favers` | yes | last 30 days | from snapshots | none |
| pub.dev | `/score`: `downloadCount30Days`, likes, points | none | last 30 days | none | none |
| Maven Central | none: Maven Central publishes no download counts | | | | |

Dependents are not shown yet: the deps.dev v3 API has no documented dependents method.

## Listing

The **Listing** tab reads the package's page on its registry live: description, README, keywords, license, home page,
repository, authors and (NuGet) icon, with readiness checks (missing README, license, repository address or icon, a
description under 20 characters, no keywords). The README is shown as text; HTML from a registry (crates.io) is reduced to
its text and never rendered. No registry has an API that edits this metadata: it ships with the package and changes with
the next release (npm, NuGet, PyPI, crates.io, RubyGems, pub.dev, Maven Central); Packagist reads `composer.json` from the
repository.

## Groups

**Package groups** (from the registries page) put packages that are released together side by side: each member's
latest version and release date, which members are behind the group's highest version, and open alerts. storeasy
suggests groups from packages that share a source repository (for example every Milvasoft.* package of
Milvasoft/Milvasoft). Groups are storeasy data; nothing changes on a registry.

## MCP

`registries_packages_list`, `registries_package_get` and `registries_alerts_list` read; `registries_package_add`,
`registries_package_remove` and `registries_package_sync` write storeasy's own data only; `registries_setup_get` and
`registries_reminders_list` read the wizard (the CI file as text) and the reminders, `registries_setup_save`,
`registries_reminder_save` and `registries_reminder_remove` save them; `registries_downloads_get`, `registries_listing_get`,
`registries_groups_list` and `registries_groups_suggest` read, `registries_group_save` and `registries_group_remove` save
groups ([mcp.md](mcp.md)).

## Not yet

Releasing from storeasy through your CI (a GitHub connection, release plans per package or group that push a tag or
dispatch the workflow, and following the run until the version appears) and registry write operations with stored,
encrypted API keys (unlist, deprecate, dist-tags, yank) come next.
