# Rules catalog

Store requirements change several times a year: new screenshot sizes, SDK minimums, policy deadlines. storeasy keeps
them as versioned data, not code, in [`rules/`](../rules/README.md), so a store change is a data update instead of a
release.

## Who reads it

| Reader | Uses it for |
|---|---|
| Readiness check (`GET apps/readiness`) | Which screenshot sets are required, satisfied by a fallback, or missing; text limits; build minimums |
| Compliance timeline (`GET apps/compliance`, `GET compliance`) | Upcoming deadlines that apply to each app (device families, SDK, target API, regions) |
| Change plans | Checking files and texts before a plan is saved (sizes, counts, character and byte limits) |
| CI intake | Package checks (target API level, SDK and Xcode minimums) |
| Studio and MCP | Export sizes; answers such as "missing: iPad 13 inch set" |

## Where the API loads it from

1. `Storeasy:Stores:RulesPath` (`Storeasy__Stores__RulesPath`), when set: a folder with `catalog.json` and the store
   files.
2. Otherwise the `rules` folder copied next to the application at build time.
3. Otherwise the copy embedded in the application.

The API validates the catalog at startup and stops if it is invalid, so a broken catalog never produces wrong readiness
answers. The readiness result records the catalog version it was evaluated against.

## Format and changes

The rule format (ids, `appliesWhen`, requirement levels, fallbacks, dates, parameters, sources), the versioning rules
and the steps to add or change a rule are in [rules/README.md](../rules/README.md). In short:

1. Check the official store source; never copy numbers from third-party sites.
2. Edit the store file, set `checkedAt`, mark anything uncertain with `verified: false` and a note.
3. Bump `catalogVersion` in `catalog.json` and every store file.
4. Run `node rules/validate.mjs`. CI runs it on every push.

## Rule watcher (planned)

A periodic job will read the stores' news and requirement pages, detect changes and file them as catalog proposals for
the instance admin; nothing is published automatically. Approved changes notify the owners of the affected apps.
