# Impact of changes (before and after)

The **Store performance → Impact** part of an app compares the days before a store change with the days after it. It is
read only and computed on every call from rows storeasy already keeps: the daily store page metrics
([Store page performance](connecting-stores.md#store-page-performance)), the tracked keyword ranks
([ASO keywords](aso-keywords.md)), applied change plans and version states. Nothing is stored and no store is called.

## Changes (events)

Each event has the UTC day it took effect, the store record, a kind, a title and a link to its plan or version.

| Source | Events | Effect day |
|---|---|---|
| Applied change plans | Listing texts, screenshots, icon (a Google Play graphic), app previews, price, category; Google Play production releases | App Store change written to a version: the day the version went live (ready for sale). Written to the live version: the day it was applied. Version not live yet: listed as *awaiting release*, not measured. Microsoft Store change: the day the next app submission was published. Google Play: the day it was applied. |
| App Store versions | A version reached ready for sale | That day |
| Microsoft Store submissions | An app submission was published | The first day storeasy saw it published |

A version and the listing written to it go live together: they are shown as related, not as confounding each other.

Sources implement `IImpactEventSource` (Application) and are registered in `AsoServiceCollectionExtensions`; every
registered source is read and merged by event id. A new kind of change (store listing experiments) is one more class and
one registration; the event kinds `ExperimentStarted` and `ExperimentEnded` and the id prefix `experiment-` are reserved
for it.

## The comparison

All arithmetic is in `ImpactMath` (pure, unit tested).

- **Windows.** Before: the `W` days ending the day before the effect day. After: the `W` days starting `gap` days after
  the effect day. `W` is 7, 14 (default) or 28 in the web app (3 to 56 in the API); the default gap of 2 skips the effect
  day and the next, because stores apply changes with a delay.
- **Counts** (impressions, page views, downloads) are compared as daily averages. Windows of 7, 14 or 28 days are whole
  weeks, so every weekday counts equally. When a window misses days (or is not whole weeks), only the weekdays both
  windows have are averaged, each weekday weighted equally, so a window with more weekend days does not look better.
- **Conversion rate** is total downloads over total page views of each window (the days that report both). Its change is
  checked with a two proportion z-test (pooled standard error, two sided p). Significant below p 0.05, likely below 0.10.
  With fewer than 100 page views in a window or 20 downloads in both, the answer is *not enough data*; a rate above 100
  percent (App Store downloads without a page view) is not a proportion and the test does not apply.
- **Keyword ranks.** For every keyword tracked for the store record (a country filter limits them), the average rank
  over the checked days the app was in the results, before and after; the average over keywords ranked in both, the
  number in the top 10, and how many moved at least one place. A keyword that entered or left the results counts as up
  or down when the other window was checked.
- **Confounding.** Other live changes of the same store record from the first day of the before window to the last day
  of the after window (the gap included) are listed with a warning.
- **Status.** *Collecting* until the after window is over, *awaiting release* for a change that is not live, *no data*
  without metrics in both windows.

## Where to find it

- Web: the app's **Store performance** tab, **Impact** chip (the timeline and the report); change markers on the Store
  performance chart and on a keyword's rank chart open the change; the organization's Store performance page lists the
  impact of recent changes.
- API: `GET aso-impact/events`, `GET aso-impact`, `GET aso-impact/recent` (`View`).
- MCP: `aso_impact_events`, `aso_impact_get`, `aso_impact_recent`.
