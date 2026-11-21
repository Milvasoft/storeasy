# ASO keywords

The "Keywords" ("Anahtar kelimeler") tab of an app researches store search keywords and tracks the app's daily rank for
them. Every score comes from the stores' public data; the organization's AI may only propose candidate terms.

## Sources

All sources are public, unauthenticated store endpoints. They are called through one client (`AsoHttp`): GET only, a
timeout (`RequestTimeoutSeconds`), one request at a time per host with a pause between requests
(`MinRequestIntervalMilliseconds`, 3000 by default; Apple asks for about 20 calls a minute), a memory cache of the answers
(`FreshnessHours`), and the User-Agent `storeasy-aso/1.0 (+https://github.com/Milvasoft/storeasy)` with no personal or
organization data. A failing source answers nothing and the rest of the research still answers.

| Store | Data | Source | Switch |
|---|---|---|---|
| App Store | Autocomplete (popularity, related terms) | `search.itunes.apple.com/WebObjects/MZSearchHints.woa/wa/hints` with the country's `X-Apple-Store-Front` storefront id | always; countries without a known storefront id get no popularity |
| App Store | Search results (difficulty, competitors, rank, up to 200) | iTunes Search API `itunes.apple.com/search?entity=software&limit=200&country=..` | always |
| Google Play | Autocomplete (popularity, related terms) | the Play Store web search box: form POST to `play.google.com/_/PlayStoreUi/data/batchexecute?rpcids=IJ4APc&hl=..&gl=..` | `GooglePlaySuggestionsEnabled` (on) |
| Google Play | Search results (competitor ids, rank) | the public search page `play.google.com/store/search?c=apps` | `GooglePlayRanksEnabled` (**off**) |
| Microsoft Store | none | not supported: there is no public search whose order can be read | |
| App Store (optional) | Popularity from Apple Search Ads | `IAsoPopularityProvider`; credentials in `Storeasy:Aso:SearchAds` | not included in this build |

**Google Play ranks are off by default.** Google Play has no search API, so ranks and competitors would come from its
search web page. Automated reading of that page may be against Google Play's terms of service and the page can change
without notice, so the operator turns `Storeasy:Aso:GooglePlayRanksEnabled` on knowingly. While it is off, Google Play
research gives popularity and related terms only, and tracking a Google Play keyword answers 409
`AsoGooglePlayRanksDisabled`.

The App Store storefront ids (`AsoCountries`) were checked on 21 Nov 2026 against a public storefront list and with live
hint calls for TR and DE. The Google Play suggestions endpoint is the one the Play Store web page itself uses; it is
undocumented (the older `market.android.com/suggest` endpoint now answers 404), so if Google changes it the answer reads
as unavailable and Google Play research shows no popularity until the reader is updated.

The iTunes Search API's order is close to, but not exactly, what the App Store app shows; ranks are read from it the same
way every day, so changes are comparable.

## Scores (0 to 100, estimates)

`Storeasy.Application.Utils.Aso.AsoScoring` holds the formulas; unit tests pin them.

- **Popularity** ("tahmini popülerlik"): how early the store's autocomplete offers the keyword. The keyword itself is
  asked first (its suggestions are also the related terms). If it is offered, a binary search over the prefix lengths
  1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, ... finds the shortest prefix that still offers it (at most `MaxPrefixQueries`
  calls in all). `popularity = 10 + 75 x ease + 15 x place`, where `ease = (length - prefix) / (length - 1)` (1 when
  one letter is enough, 0 when only the whole keyword brings it) and `place = 1 - index / suggestion count` (1 for the
  first suggestion). A keyword the autocomplete never offers scores 5 when the store suggests other terms for it, else 0.
  When Search Ads credentials and a provider are present, its popularity is used instead.
- **Difficulty**: how strong the first ten results are.
  `difficulty = 100 x (0.55 x ratings + 0.10 x stars + 0.20 x title match + 0.15 x crowding)` with `ratings` the mean of
  `min(1, log10(1 + rating count) / 5)` (100,000 ratings count fully), `stars` the mean average rating / 5, `title match`
  the share of the ten whose name contains every word of the keyword, and `crowding = min(1, result count / 200)`. No
  results gives 0; results without ratings (Google Play page) give no difficulty.
- **Opportunity**: `sqrt(popularity x (100 - difficulty))`, the geometric mean of demand and room; unknown when either is.
- **Competing apps**: the number of results the search returned (at most 200).

Measurements are saved per store, country and keyword (`AsoKeywordMetrics`, shared by every organization because the
store's public answer is the same for everyone) and reused while younger than `FreshnessHours`.

## Research runs as a job

Measuring is slow on purpose: every store host gets one request at a time with `MinRequestIntervalMilliseconds` (3
seconds) between them, and a new keyword needs up to `MaxPrefixQueries` autocomplete calls plus one search. Twenty new
keywords take a few minutes, longer than the reverse proxy waits for a request (about 100 seconds), so research never
runs inside the request.

- `POST aso-keywords/research` (app, store, country, 1 to 20 keywords; `View`, 60 an hour per user) checks the input,
  normalizes the keywords and queues a store job of kind `AsoKeywordResearch` (110) for the app's store record. It
  answers the job id at once. The same request while an identical job (same store record, country, language and
  keywords) is still queued or running answers that job's id instead of queuing a second one. The job has two attempts:
  the second one only runs when the runner stopped during the first; a store that did not answer is not retried.
- The job (`AsoKeywordResearchJobHandler`) measures the keywords one by one with `IAsoKeywordResearcher`, so the pacing,
  the per host gate, the answer cache and the saved measurements (`AsoKeywordMetrics`, reused while younger than
  `FreshnessHours`) are the same as before. Its one step, `keywords.measure`, reports "measured / total" after each
  keyword; the live stream carries it as job step events. A cancel request (`StoreJobs/job/cancel`, `EditContent`) is
  seen at the next progress report, so the job stops after the keyword it is measuring. When no store source answered
  for any keyword the job fails with the reason that `GET research` shows as the localized `AsoSourceUnavailable`
  message. On success the measurements (the same fields as before, in the order asked) are saved as the job's result.
- `GET aso-keywords/research` (app and store, optional job id; `View`) answers the job's status, `Measured` and `Total`,
  the error of a failed job and, once it succeeded, the measured keywords with whether each is tracked now. Without a
  job id it answers the app's latest research in that store, or no data when there was none. No store is called.

The "Keywords" tab starts the job with "Measure" ("Ölç"), shows "Measuring 5 of 20" ("Ölçülüyor 5/20") with a progress
bar and a "Stop" button for editors, and reads the job again on its live events and every three seconds while it runs.
The job is watched, so its end is announced with a toast on any page. When the tab is opened again it shows the app's
latest research: still running, its result, why it failed, or that it was stopped. Keywords selected from a result are
tracked in the country they were measured in.

The other calls of the tab stay synchronous and fit the proxy's limit: seeds make three paced store calls and one
listing read (about 15 seconds), tracking calls no store (the hourly rank job reads the ranks), and the AI ideas call
waits at most 75 seconds for the AI's answer, then answers `AiUnavailable`.

## Seeds

`GET aso-keywords/seeds` gives candidates from data: the listing of the country's language (name and subtitle parts and
words, App Store keywords field terms, the description's most used words and word pairs), words that two or more of the
top competitors' names share (one search of the first listing seed), and the store's suggestions for the first two
listing seeds. `POST aso-keywords/seeds/suggest` asks the organization's AI (the key and model of the reply drafts) for
more ideas grouped by theme; they are candidates only and audited as `AsoKeywordSeedsGenerated` with tokens and cost.

## Tracking

`POST aso-keywords/track` adds keywords for one store record and country (normalized: trimmed, single spaces, lower case
in the country's language). At most `MaxTrackedKeywordsPerOrganization` active keywords per organization (200 by
default; 409 `AsoKeywordLimitReached`). With `TrackCompetitors`, the chosen competitor ids (or, when none are given, the
top five results of the first check other than the app) are recorded too. `POST aso-keywords/untrack` stops the checks
and keeps the history; tracking the same keyword again continues it. Both are audited (`AsoKeywordsTracked`,
`AsoKeywordsUntracked`).

`AsoRankJob` runs every hour (minute 20, UTC) and checks the active keywords not yet checked that UTC day, at most
`MaxRankChecksPerRun` per run, with one store search per store, country and keyword shared by every organization. It
writes one `AsoKeywordRanks` row per keyword and day (`Rank` null = not in the first `Depth` results) and one
`AsoCompetitorRanks` row per followed competitor, and refreshes the keyword's difficulty and top results from the same
search. A keyword whose search failed is not marked checked, so the next run tries it again.

## Rank series

`IAsoRankSeries.GetRankSeriesAsync(storeAppId, keyword, country, from, to)` returns one point per UTC day with `Checked`
and `Rank`: a day without a check is `Checked = false`, a checked day without a rank means "not in the results read". It
finds the rows by store record, keyword and country, so it is the query other features (such as a before/after report)
read. `GET aso-keywords/ranks` serves the same series with the followed competitors and the latest top ten.

## Apple keywords field helper

`GET aso-keywords/coverage` reads the App Store record's texts live and tells, per listing language, which tracked
keywords the name, subtitle and keywords field cover (all words present in one of them), which words are missing and how
many characters they need, which keywords field words are spent twice (already in the name or subtitle, or repeated),
and how many of the 100 characters are left.

## Configuration

```json
"Storeasy": {
  "Aso": {
    "MaxTrackedKeywordsPerOrganization": 200,
    "RequestTimeoutSeconds": 10,
    "MinRequestIntervalMilliseconds": 3000,
    "FreshnessHours": 24,
    "RankDepth": 200,
    "MaxPrefixQueries": 5,
    "MaxRankChecksPerRun": 300,
    "GooglePlaySuggestionsEnabled": true,
    "GooglePlayRanksEnabled": false,
    "SearchAds": { "ClientId": "", "TeamId": "", "KeyId": "", "PrivateKey": "", "OrgId": "" }
  }
}
```

## MCP

`aso_keywords_research`, `aso_keywords_research_result`, `aso_keywords_seeds`, `aso_keywords_ideas`,
`aso_keywords_track`, `aso_keywords_untrack`, `aso_keywords_tracked`, `aso_keywords_ranks`, `aso_keywords_coverage`.

`aso_keywords_research` queues the research job and returns its id; `aso_keywords_research_result` reads its progress
and, once it succeeded, the measurements (call it again while the job is queued or running; `jobs_get` and
`jobs_cancel` work on it too).
