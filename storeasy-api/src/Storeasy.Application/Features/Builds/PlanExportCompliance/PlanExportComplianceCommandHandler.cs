using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Builds.PlanExportCompliance;

/// <summary>
/// Handles <see cref="PlanExportComplianceCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record PlanExportComplianceCommandHandler(IStoreContentService ContentService,
                                                 IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<PlanExportComplianceCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanExportComplianceCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Release, cancellationToken, request.VersionId, StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (read.Snapshot.Build == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.BuildNotFound);

        var desired = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Build, null, "usesNonExemptEncryption")] = request.UsesNonExemptEncryption ? "true" : "false",
        };

        var draft = FieldPlanner.Build(ChangePlanKind.ExportCompliance, $"Export compliance of build {read.Snapshot.Build.BuildNumber}", read.Snapshot, desired, g => new Storeasy.Domain.JsonModels.PlanCall
        {
            Method = "PATCH",
            Path = "/v1/builds",
            Target = read.Snapshot.Build.BuildNumber,
        });

        draft.Changes.Fields.Add(new FieldChange(Storeasy.Domain.Content.ContentGroups.Build, null, "buildId", read.Snapshot.Build.Id));

        if (draft.Items.Count == 0)
            draft.Changes = new ContentChangeSet();

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
