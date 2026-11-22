# Store rules catalog

Store rules for App Store Connect, Google Play, Microsoft Store, winget and the Chrome Web Store, kept as data rather than code.
The storeasy API loads the catalog, the readiness check and the per-app compliance timeline evaluate it,
and the rule watcher proposes changes to it. See `docs/urun-plani.md` section 12 and
[docs/rules-catalog.md](../docs/rules-catalog.md).

## Structure

```
rules/
  catalog.json                 manifest: catalogVersion, generatedAt, list of store files
  apple.json                   App Store Connect rules
  google.json                  Google Play rules
  microsoft.json               Microsoft Store rules
  winget.json                  winget (microsoft/winget-pkgs) rules
  chrome.json                  Chrome Web Store rules
  schema/catalog.schema.json   JSON Schema (draft 2020-12) for the manifest and the store files
  validate.mjs                 validator, no dependencies
```

Each rule has:

- `id`: stable, `<store>.<category>.<slug>` (for example `apple.screenshots.ipad-13`). Never rename or reuse an id.
  To retire a rule, add `deprecated` (with `since` and optionally `replacedBy`) and keep it for at least one minor version.
- `store`, `category`, `title` (`en` and `tr`), `description` (`en`, optional `tr`).
- `appliesWhen`: conditions on the app. Keys are combined with AND, values inside one list with OR
  (platforms, device families, distribution regions, developer regions, app type, package type, new app or update,
  feature flags, build SDK). No key means no restriction.
- `requirement`: `level` is `required`, `required-from` (with `from` date), `recommended` or `optional`.
  `fallbackTo` lists the rules the store derives a missing asset from; `satisfiedBy` lists rules that also meet the requirement.
- `effectiveDate`, `deadline` (ISO dates or null) and `dateNote` when the source only gives an approximate date.
- `params`: machine-checkable values (pixel sizes, counts, character or byte limits, target SDK, Xcode version...).
- `check.kind`: `automatic` (decided from store API or build data), `assisted` (checklist step), `info` (timeline only),
  and `check.inputs` naming the data the evaluator needs.
- `sources`: at least one official URL with `checkedAt`. `verified` is `false` when any fact could not be confirmed;
  `notes` then says what. `conflicts` records disagreements between official sources and the reading the catalog uses.

## Adding or changing a rule

1. Check the official source. Do not copy numbers from third-party sites.
2. Edit the store file. Set `checkedAt` to the day you checked; set `verified: false` with a note if anything is uncertain.
3. Bump `catalogVersion` in `catalog.json` and in every store file (they must match), and update `generatedAt`.
4. Run `node rules/validate.mjs` and fix every error.

JSON files are UTF-8 without BOM, LF line endings, two-space indent. Avoid em and en dashes in text (the validator rejects them).

## Versioning

`catalogVersion` is semver-like and changes on every change to the data:

- MAJOR: schema change that breaks readers (renamed or removed field, changed meaning).
- MINOR: rules added or deprecated, or a requirement, date or value changed.
- PATCH: wording, notes, sources or `checkedAt` only.

The readiness check stores the catalog version it evaluated against, so results stay explainable after an update.

## License

The catalog data is part of storeasy and is licensed under the repository license, AGPL-3.0-only (see `LICENSE` and `NOTICE`
at the repository root). Rule texts summarize public store documentation; follow the linked sources for the authoritative wording.
