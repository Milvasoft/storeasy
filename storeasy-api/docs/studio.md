# Mockup studio (phase 6)

Server side mockup and screenshot studio: templates per app, previews and exports rendered from one template JSON with
SkiaSharp (docs/urun-plani.md 2.4 and K8, design `12-Studyo.html`). This page documents the template schema, the
endpoints, the export manifest the screenshot upload plan consumes, the frames, the fonts and the deployment notes.

## Endpoints

All under `/api/v1.0/mockups`, organization scoped (`organizationId` in the body or query). Read and preview need `View`,
writes and exports need `EditContent`.

| Method and path | Request | Answer |
|---|---|---|
| `PATCH templates` | ListRequest + `appId?` | `MockupTemplateListDto` page |
| `GET template?templateId=` | | `MockupTemplateDetailDto` (spec, slots, captions, `version`) |
| `POST template` (Idempotency-Key) | `appId, name, locales[], primaryLocale, slotCount?, starterKey?, copyFromTemplateId?, copyCaptions` | template id |
| `PUT template` | `templateId, version, name, spec, locales[], primaryLocale, slotCount, slots[{order, family, locale?, assetId}]` | new version; 409 `MockupTemplateChanged` when `version` is stale |
| `DELETE template?templateId=` | | template id (soft delete) |
| `PUT captions` | `templateId, items[{locale, slotOrder, title, subtitle}]` (empty cell removes it) | `{ saved, measuredClassKey, warnings[] }` (overflow in the narrowest exported class) |
| `POST captions/suggest` | `templateId, targetLocale, sourceLocales?, slotOrders?` | suggestions, never saved; 503 when the `Studio.AiCaptions` flag is off or no `ITitleTranslator` is configured (the default is a no-op) |
| `GET frames` | | frame kinds with defaults and the catalog classes drawn with each |
| `GET starter-templates` | | built in starters |
| `POST render-preview` | `templateId, locale?, classKey?, slotOrders? (max 4), spec? (unsaved), maxWidth?` | `{ images[{slotOrder, width, height, url (data:image/png;base64), warnings[]}], durationMs }` |
| `POST export/plan` | `templateId, locales?, classKeys?, targets?[{classKey, width, height}]` | `MockupExportPlanDto` (items with status and selection, rejected sizes, total) |
| `POST export` (Idempotency-Key) | same as plan | StoreJob id (`Kind = MockupExport`) |
| `GET export/manifest?jobId=` | | `MockupExportManifest` (below); 409 until the job succeeded |
| `GET export/zip?jobId=` | | presigned URL of `mockups-{jobId}.zip` |

The plan is stateless: the export takes the same selection and plans again, so it renders exactly what the plan showed.
Defaults: every locale of the template; the classes the catalog marks required or recommended for the app's stores
(plus optional classes that stand in for a required one, e.g. the 6.9" iPhone class for Face ID large). A size the class
does not accept is listed in `rejected` (1320x2868 for the 6.1"/6.3" class) and the class's export size is used.

Export progress streams on `/hubs/activity` like every StoreJob: `assets.load`, one `render.size` step per class (progress
per image), `asset.store`, `zip.store`. An image that cannot be drawn (raw screenshot needs more than 125% enlargement,
unreadable file) fails its class's step and is listed in the manifest's `failures`; the job still succeeds with the other
images. Cancelling deletes the objects already written. Audit: `MockupTemplateCreated/Updated/Deleted`,
`MockupCaptionsUpdated`, `MockupExported`.

## Template schema (`spec`, schemaVersion 1)

Stored as jsonb in `MockupTemplates.Spec`; validated by `MockupSpecRules.Validate` (400 with the path of every problem).
Lengths are fractions: horizontal of the canvas width, vertical of the canvas height, type and frame sizes of the shorter
canvas side (the unit). One design therefore renders at every catalog size.

```json
{
  "schemaVersion": 1,
  "canvas": { "classKey": "apple.screenshots.iphone-dynamic-island-medium" },
  "background": { "kind": "solid | gradient | image", "colors": ["#4636FF", "#1B1464"], "angle": 90, "assetId": null },
  "frame": { "kind": "auto | none | phone | tablet | foldable | desktop", "cutout": "island | notch | hole | none",
             "color": "#1C1C22", "cornerRadius": null, "bezel": null, "scale": 0.78, "offsetX": 0, "offsetY": 0.27,
             "rotation": 0, "shadow": true },
  "text": { "font": "inter | noto-sans", "color": "#FFFFFF", "position": "top | bottom", "alignment": "center | start | end",
            "margin": 0.08, "gap": 0.025, "uppercase": false,
            "title":    { "size": 0.085, "weight": 700, "maxLines": 2, "autoFit": true, "minSize": 0.055, "lineHeight": 1.15, "color": null },
            "subtitle": { "size": 0.048, "weight": 400, "maxLines": 2, "autoFit": true, "minSize": 0.036, "lineHeight": 1.25 } },
  "layers": [
    { "type": "shape", "shape": "rect | round-rect | ellipse", "x": 0.05, "y": 0.42, "width": 0.9, "height": 0.42, "radius": 0, "color": "#4636FF", "opacity": 0.14, "above": false },
    { "type": "badge", "x": 0.8, "y": 0.3, "color": "#FFD25A", "textColor": "#1B1464", "textSize": 0.04, "above": true, "texts": { "*": "NEW", "tr": "YENI" } }
  ]
}
```

Captions are not in the spec: `MockupCaptions` holds title and subtitle per locale and slot; an empty title is drawn
with the primary locale's caption (warning `CaptionFallback`). Raw screenshots are `MockupSlots` rows per slot and device
family (`Phone`, `Tablet`, `Desktop`), optionally per locale; a class without its own family uses the phone screenshot
(warning `FamilyFallback`; a phone screenshot in a desktop window is fitted inside, `AspectMismatch`).

Text: runs are split by the first embedded font that has each character, shaped with HarfBuzz, broken greedily at
spaces, after hyphens and between Chinese or Japanese characters (no break before closing punctuation or small kana,
after opening brackets), grapheme splitting for a piece wider than the line (long words, Thai). Auto fit shrinks in 5%
steps to `minSize`; beyond that the caption overflows (`CaptionOverflow` with `lines` and `maxLines`). Upper casing uses
the locale's culture (Turkish dotted I). Right to left paragraphs (first strong character Arabic or Hebrew) draw their
runs right to left and `start` aligns right. A weight the fonts do not embed is drawn with the nearest one (`FontMissing`).

Output: opaque PNG (color type RGB, no alpha channel) at the exact class size; transparent pixels of a raw screenshot are
flattened (`AlphaFlattened`). Rendering is deterministic on one platform; the golden image tests compare with a tolerance
across platforms (`tests/Storeasy.UnitTests/StudioTests/Goldens`, rewrite with `STOREASY_UPDATE_GOLDENS=1`).

## Export manifest (hand over to the screenshot upload plan)

The job's `Result` (PascalCase in the database, camelCase from `GET export/manifest` and in the zip's `manifest.json`):

```json
{
  "schemaVersion": 1, "jobId": 8812, "templateId": 12, "appId": 3, "templateVersion": 4, "catalogVersion": "1.0.1",
  "createdDate": "2026-11-18T14:21:00Z", "zipAssetId": 991, "imageCount": 159, "totalBytes": 148600000,
  "sets": [
    { "store": 1, "classKey": "apple.screenshots.iphone-dynamic-island-medium", "locale": "tr", "width": 1206, "height": 2622,
      "images": [ { "slotOrder": 1, "assetId": 901, "size": 912345, "sha256": "...", "md5": "...", "zipPath": "apple/iphone-dynamic-island-medium/tr/01.png" } ] }
  ],
  "failures": [ { "classKey": "apple.screenshots.ipad-13", "locale": "tr", "slotOrder": 4, "code": "AssetTooSmall", "message": "..." } ],
  "warnings": [ { "classKey": "...", "locale": "de-DE", "slotOrder": 2, "code": "CaptionOverflow", "message": "..." } ]
}
```

One set is one store screenshot set: an App Store display type in a localization, a Play image type in a language, a
Microsoft Store device family in a language. `classKey` is the rules catalog id (`store` is `StoreKind`: 1 Apple,
2 Google, 3 Microsoft), images are in display order, every asset is `Purpose = MockupOutput`, `Status = Uploaded`, PNG
without alpha. A consumer builds its upload plan per set (diff by `md5`/`sha256` against the store's current set) and
leaves failed slots to the store's current images. The feature graphic (`google.graphics.feature-graphic`, 1024x500) is
one image per locale drawn from slot 1's caption without a device.

## Screenshot classes and sizes

`IScreenshotSizeSource` reads the rules catalog (`rules/*.json`); `FileScreenshotSizeSource` is the file based
implementation (folder: `Storeasy:Studio:RulesPath`, else `rules` next to the application, else the nearest `rules`
folder above it). Once the readiness check's `IRulesCatalog` is merged, an implementation over it replaces this one; only
`StudioClassMap` (which rule ids the studio exports and with which frame) stays in code. Export size: the largest listed
size of the rule; rules that give ranges carry a preset that is checked against the range and aspect ratio when the
catalog loads.

| Class key | Export size | Frame |
|---|---|---|
| apple.screenshots.iphone-dynamic-island-medium | 1206x2622 | Phone |
| apple.screenshots.iphone-dynamic-island-large | 1320x2868 | Phone |
| apple.screenshots.iphone-face-id-large | 1284x2778 | Phone (optional when the 6.9" class is exported) |
| apple.screenshots.ipad-13 | 2064x2752 | Tablet |
| apple.screenshots.iphone-duo-outer | 1398x2034 | Foldable outer |
| apple.screenshots.iphone-duo-inner | 2007x2853 | Foldable inner (crease hint) |
| google.screenshots.phone | 1080x1920 (preset) | Phone |
| google.screenshots.tablet-7 | 1080x1920 (preset, catalog says 16:9) | Tablet |
| google.screenshots.tablet-10 | 1440x2560 (preset, 16:9) | Tablet |
| google.graphics.feature-graphic | 1024x500 | none |
| microsoft.screenshots.desktop | 1920x1080 (preset, 1366x768 minimum) | Desktop window |

The design file shows 1200x1920 and 1600x2560 for the Play tablets; the catalog requires 16:9, so the studio uses 9:16
presets of the same class.

## Frames

storeasy's own drawings in code (`DeviceFramePainter`), no manufacturer artwork, logo or product shape: a rounded body in
the template's color with an edge light and soft shadow, an even bezel, side keys on phones. Phone cutouts: floating pill
(island), notch, camera hole, none. Tablet: camera dot in the bezel. Foldable outer: hole and a hinge edge. Foldable inner:
wide body with a crease hint along the fold and a hole on the right panel. Desktop: window with a title bar and generic
line glyphs. Corner radius and bezel are fractions of the body width; the template's values are given for the phone and
scale the other kinds by the same ratio.

## Fonts and licenses

Shipped in `src/Storeasy.Infrastructure/Studio/Fonts` and copied next to the application as `StudioFonts/` (override with
`Storeasy:Studio:FontsPath`). No system font is used. All SIL Open Font License 1.1, license text next to the files:

| Family | Files | Source |
|---|---|---|
| Inter 4.1 | Regular, Medium, SemiBold, Bold, ExtraBold | github.com/rsms/inter release 4.1 |
| Noto Sans | Regular, Medium, SemiBold, Bold, ExtraBold | github.com/notofonts (latin-greek-cyrillic) |
| Noto Sans Arabic, Hebrew, Thai, Devanagari | Regular, Bold | github.com/notofonts |
| Noto Sans CJK JP, KR, SC, TC | Regular, Bold (subset OTF) | github.com/notofonts/noto-cjk |

The CJK files are about 45 MB of the 51 MB; they cover Japanese, Korean and both Chinese scripts. Libraries: SkiaSharp,
SkiaSharp.HarfBuzz, HarfBuzzSharp (MIT; Skia BSD-3-Clause, HarfBuzz Old MIT), all AGPL-3.0 compatible.

## Deployment notes

- Native assets: `SkiaSharp.NativeAssets.Linux.NoDependencies` (no fontconfig or freetype from the OS) and
  `HarfBuzzSharp.NativeAssets.Linux` carry `linux-arm64` and `linux-x64` libraries; `dotnet publish` puts them under
  `runtimes/` and the runtime picks the one of the host. The chiseled `aspnet:*-chiseled-extra` image has the glibc and
  libstdc++ they need; no package install and no Dockerfile change is needed.
- The image grows by about 51 MB (fonts) plus about 10 MB of native libraries per architecture.
- The rules catalog is outside the API's Docker build context: mount it and set `Storeasy__Studio__RulesPath` (the
  readiness check's catalog loader copies it next to the application when built from the repository root). Without it
  the studio's catalog calls answer 500 while the rest of the API runs.
- Exports use the bucket (`Storage:S3`); without one, export and previews with screenshots answer 503. The zip is built
  in a temporary file, not in memory.
- `Storeasy:Studio:PreviewMaxWidth` (800) and `AssetCacheMegabytes` (128, in-memory LRU of raw screenshots) tune the
  previews.
