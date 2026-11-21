using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// Writes daily metric rows with one <c>INSERT ... ON CONFLICT DO UPDATE</c> over <c>unnest</c> arrays, so a read of the
/// same days again (every sync re-reads the last week) updates the rows instead of adding to them. A metric the new row
/// does not report keeps its stored value (the App Store's two reports arrive separately).
/// </summary>
/// <param name="dbContext"></param>
/// <param name="timeProvider"></param>
public sealed class StoreMetricWriter(StoreasyDbContext dbContext, TimeProvider timeProvider)
{
    private const string _upsertSql = """
        INSERT INTO "StoreMetrics" ("OrganizationId", "StoreAppId", "Store", "Date", "Country", "Source", "Impressions", "PageViews", "Downloads", "Redownloads", "UpdatedDate")
        SELECT {0}, {1}, {2}, t.d, t.c, t.s, t.i, t.p, t.dl, t.r, {3}
        FROM unnest({4}::date[], {5}::text[], {6}::smallint[], {7}::bigint[], {8}::bigint[], {9}::bigint[], {10}::bigint[]) AS t(d, c, s, i, p, dl, r)
        ON CONFLICT ("StoreAppId", "Date", "Country", "Source") DO UPDATE SET
            "Impressions" = COALESCE(EXCLUDED."Impressions", "StoreMetrics"."Impressions"),
            "PageViews" = COALESCE(EXCLUDED."PageViews", "StoreMetrics"."PageViews"),
            "Downloads" = COALESCE(EXCLUDED."Downloads", "StoreMetrics"."Downloads"),
            "Redownloads" = COALESCE(EXCLUDED."Redownloads", "StoreMetrics"."Redownloads"),
            "UpdatedDate" = EXCLUDED."UpdatedDate"
        """;

    private readonly StoreasyDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Upserts the rows of one store record and returns how many rows were written.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="store"></param>
    /// <param name="rows"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<int> UpsertAsync(int organizationId, int storeAppId, StoreKind store, IReadOnlyList<StoreMetricRow> rows, CancellationToken cancellationToken)
    {
        // One row per key: a duplicate key in one statement would make ON CONFLICT touch a row twice.
        var unique = rows.GroupBy(r => (r.Date, Country: StoreMetricRules.NormalizeSlice(r.Country), r.Source)).Select(g => g.Last() with { Country = g.Key.Country }).ToList();

        if (unique.Count == 0)
            return 0;

        object[] parameters =
        [
            organizationId,
            storeAppId,
            (short)store,
            _timeProvider.GetUtcNow().UtcDateTime,
            unique.Select(r => r.Date).ToArray(),
            unique.Select(r => r.Country).ToArray(),
            unique.Select(r => (short)r.Source).ToArray(),
            unique.Select(r => r.Impressions).ToArray(),
            unique.Select(r => r.PageViews).ToArray(),
            unique.Select(r => r.Downloads).ToArray(),
            unique.Select(r => r.Redownloads).ToArray(),
        ];

        return await _dbContext.Database.ExecuteSqlRawAsync(_upsertSql, parameters, cancellationToken);
    }
}
