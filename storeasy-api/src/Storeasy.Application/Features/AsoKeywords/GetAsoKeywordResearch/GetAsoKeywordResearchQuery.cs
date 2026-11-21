using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordResearch;

/// <summary>
/// A keyword research job of an app in one store (<c>POST aso-keywords/research</c>): its state, the keywords measured so
/// far and, once it succeeded, the measurements with the current tracking state. Without a job id, the app's latest
/// research in that store, so a page opened again shows the research still running or its result; no data when there was
/// none. Nothing is asked of a store.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoKeywordResearchQuery : IQuery<AsoKeywordResearchJobDto>, IOrganizationRequest, IAppStoreRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Research job id; the latest research of the app in the store when empty.
    /// </summary>
    public long? JobId { get; set; }
}
