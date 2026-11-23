using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.DetectReleaseSettings;

/// <summary>
/// Handles <see cref="DetectReleaseSettingsQuery"/>.
/// </summary>
/// <param name="planService"></param>
public class DetectReleaseSettingsQueryHandler(IRegistryPlanService planService) : IInterceptable, IQueryHandler<DetectReleaseSettingsQuery, List<ReleaseSuggestionDto>>
{
    private readonly IRegistryPlanService _planService = planService;

    /// <inheritdoc/>
    public Task<Response<List<ReleaseSuggestionDto>>> Handle(DetectReleaseSettingsQuery request, CancellationToken cancellationToken)
        => _planService.DetectAsync(request.OrganizationId, request.TrackedPackageId, request.PackageGroupId, request.Repository?.Trim(), cancellationToken);
}
