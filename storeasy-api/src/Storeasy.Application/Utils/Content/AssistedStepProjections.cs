using Storeasy.Domain.Apps;
using System.Linq.Expressions;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Column sets read from <see cref="AssistedStep"/> rows.
/// </summary>
public static class AssistedStepProjections
{
    /// <summary>
    /// Everything a step detail or hand-off shows.
    /// </summary>
    public static Expression<Func<AssistedStep, AssistedStep>> Detail { get; } = s => new AssistedStep
    {
        Id = s.Id,
        AppId = s.AppId,
        Store = s.Store,
        Key = s.Key,
        Status = s.Status,
        DoneItems = s.DoneItems,
        HandedOffDate = s.HandedOffDate,
        HandedOffByUserName = s.HandedOffByUserName,
        CompletedDate = s.CompletedDate,
        CompletedByUserName = s.CompletedByUserName,
        CompletedChannel = s.CompletedChannel,
        EvidenceAssetId = s.EvidenceAssetId,
        Note = s.Note,
    };
}
