using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Releases.PlanRelease;

/// <summary>
/// Handles <see cref="PlanReleaseCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="BuildRepository"></param>
[Log]
public record PlanReleaseCommandHandler(IStoreContentService ContentService,
                                        IChangePlanService ChangePlanService,
                                        IStoreasyRepositoryBase<Build> BuildRepository) : IInterceptable, ICommandHandler<PlanReleaseCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanReleaseCommand request, CancellationToken cancellationToken)
    {
        var store = request.Kind == ReleasePlanKind.SetReleaseType ? StoreKind.Apple : StoreKind.Google;
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Release, cancellationToken, request.VersionId, store);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (request.Kind == ReleasePlanKind.SetReleaseType)
        {
            var settings = ReleasePlanner.AppleReleaseSettings(read.Snapshot, request.ReleaseType, request.EarliestReleaseDate, request.PhasedRelease);

            return await _changePlanService.SaveAsync(read.App, read.Snapshot, settings, request.DryRun, request.Confirm, cancellationToken);
        }

        var codes = request.VersionCodes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        var known = await _buildRepository.GetAllAsync(b => b.StoreAppId == request.StoreAppId && b.OrganizationId == request.OrganizationId && codes.Contains(b.ExternalId),
                                                       projection: b => new Build { ExternalId = b.ExternalId },
                                                       cancellationToken: cancellationToken);

        var draft = ReleasePlanner.PlayRelease(read.Snapshot, new TrackReleaseChange
        {
            Track = request.Track.Trim(),
            Name = request.Name,
            VersionCodes = codes,
            Status = request.Status,
            UserFraction = request.UserFraction,
            ReleaseNotes = request.ReleaseNotes,
            Countries = request.Countries,
            IncludeRestOfWorld = request.IncludeRestOfWorld,
        }, null);

        foreach (var code in codes.Where(c => known.All(k => k.ExternalId != c)))
            draft.Warnings.Add(new Storeasy.Domain.JsonModels.PlanWarning { Key = "Plan.UnknownVersionCode", Message = code, Field = "versionCodes" });

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, request.Confirm, cancellationToken);
    }
}
