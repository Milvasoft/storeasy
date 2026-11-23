# Package registries

storeasy watches the packages you publish to npm, NuGet, PyPI and crates.io: the latest version, when it was published,
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

Adding, removing and **Sync now** need the Edit content permission; every member can read.

## What storeasy reads

| Registry | Source | Versions | Provenance and CI run |
|---|---|---|---|
| npm | `registry.npmjs.org/<name>` | `versions`, `time`, `deprecated` per version, `dist-tags.latest` | `dist.attestations.provenance` per version; the SLSA provenance of the latest version (`/-/npm/v1/attestations/<name>@<version>`) names the source repository, the workflow and the run |
| NuGet | registration index (`registration5-gz-semver2`) | `version`, `published`, `listed`, `deprecation` | none: NuGet has no provenance, so storeasy shows "Not offered" and raises no provenance alert |
| PyPI | `pypi.org/pypi/<name>/json` | `releases` with upload time, `yanked` and `yanked_reason` | the Integrity API (`/integrity/<project>/<version>/<file>/provenance`) names the repository and the workflow; PyPI does not name the run. The latest version is checked on every sync, older ones a few per sync |
| crates.io | `crates.io/api/v1/crates/<name>` | `num`, `created_at`, `yanked`, `yank_message` | `trustpub_data` of trusted publishing (GitHub run or GitLab job) |
| OSV.dev | `api.osv.dev/v1/querybatch` | | security advisories of the latest version (id, severity, summary) |

The latest version is the registry's own: npm `dist-tags.latest`, crates.io's highest stable version, PyPI's current
release and NuGet's highest listed stable version.

## Alerts

| Alert | Open while |
|---|---|
| Deprecated | the latest version is deprecated, or a version was deprecated after you started tracking the package |
| Yanked | a version was yanked (or unlisted on NuGet) after you started tracking the package |
| No provenance | the latest version was published without provenance (npm, PyPI, crates.io) |
| Security advisory | OSV.dev reports an advisory for the latest version; when OSV.dev cannot be read the alert stays as it was |

An alert is resolved by the sync that sees its condition gone. A new version, a newly deprecated or yanked version and a
new advisory are written to the audit log and notified to the organization's editors (notification kind
"Package registries", in the app by default).

## Sync

Every hour storeasy queues a sync for the packages not read in the last six hours, so every package is read about every
six hours; **Sync now** reads one package (or all of them) at once. A package the registry cannot be read for keeps its
data and shows the error.

## MCP

`registries_packages_list`, `registries_package_get` and `registries_alerts_list` read; `registries_package_add`,
`registries_package_remove` and `registries_package_sync` write storeasy's own data only ([mcp.md](mcp.md)).

## Not yet

The trusted publishing setup wizard and warnings about expiring registry tokens (they need registry credentials) come
later, as do Maven Central, RubyGems, Packagist and pub.dev.
