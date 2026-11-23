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

## MCP

`registries_packages_list`, `registries_package_get` and `registries_alerts_list` read; `registries_package_add`,
`registries_package_remove` and `registries_package_sync` write storeasy's own data only; `registries_setup_get` and
`registries_reminders_list` read the wizard (the CI file as text) and the reminders, `registries_setup_save`,
`registries_reminder_save` and `registries_reminder_remove` save them ([mcp.md](mcp.md)).

## Not yet

The trusted publishing setup wizard and warnings about expiring registry tokens (they need registry credentials) come
later, as do Maven Central, RubyGems, Packagist and pub.dev.
