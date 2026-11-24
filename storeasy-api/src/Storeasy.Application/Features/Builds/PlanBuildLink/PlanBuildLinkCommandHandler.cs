using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Builds.PlanBuildLink;

/// <summary>
/// Handles <see cref="PlanBuildLinkCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="BuildRepository"></param>
[Log]
public record PlanBuildLinkCommandHandler(IStoreContentService ContentService,
                                          IChangePlanService ChangePlanService,
                                          IStoreasyRepositoryBase<Build> BuildRepository) : IInterceptable, ICommandHandler<PlanBuildLinkCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanBuildLinkCommand request, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.GetFirstOrDefaultAsync(b => b.Id == request.BuildId && b.StoreAppId == request.StoreAppId && b.OrganizationId == request.OrganizationId,
                                                                  projection: b => new Build { ExternalId = b.ExternalId, ProcessingState = b.ProcessingState, BuildNumber = b.BuildNumber },
                                                                  cancellationToken: cancellationToken);

        if (build == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.BuildNotFound);

        if (build.ProcessingState != BuildProcessingState.Valid)
            return Response<ChangePlanDto>.Error(null, MessageKey.BuildNotValid);

        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Release, cancellationToken, request.VersionId, StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (!read.Snapshot.VersionEditable)
            return Response<ChangePlanDto>.Error(null, MessageKey.VersionNotEditable);

        var draft = ReleasePlanner.BuildLink(read.Snapshot, build.ExternalId, build.BuildNumber, request.UsesNonExemptEncryption);

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
