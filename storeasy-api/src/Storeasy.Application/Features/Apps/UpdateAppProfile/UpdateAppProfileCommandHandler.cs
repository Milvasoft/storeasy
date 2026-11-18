using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.UpdateAppProfile;

/// <summary>
/// Handles <see cref="UpdateAppProfileCommand"/>.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateAppProfileCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateAppProfileCommand, int>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateAppProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = new AppProfile
        {
            Platforms = Normalize(request.Platforms),
            DeviceFamilies = Normalize(request.DeviceFamilies),
            Regions = request.Regions?.Select(r => r.Trim().ToUpperInvariant()).Where(r => r.Length > 0).Distinct().ToList(),
            DeveloperRegion = request.DeveloperRegion?.Trim().ToUpperInvariant(),
            AppType = request.AppType?.Trim().ToLowerInvariant(),
            Features = Normalize(request.Features),
            BuildSdkVersion = request.BuildSdkVersion?.Trim(),
            TargetSdk = request.TargetSdk,
            IsLive = request.IsLive,
        };

        var updated = await _appRepository.ExecuteUpdateAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              new Milvasoft.DataAccess.EfCore.Utils.SetPropertyBuilder<App>().SetPropertyValue(a => a.Profile, profile),
                                                              cancellationToken);

        if (updated == 0)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.AppProfileUpdated, request.AppId.ToString(), $"Catalog profile of app {request.AppId} updated.", details: profile, cancellationToken: cancellationToken);

        return Response<int>.Success(request.AppId);

        static List<string> Normalize(List<string> values) => values?.Select(v => v.Trim().ToLowerInvariant()).Where(v => v.Length > 0).Distinct().ToList();
    }
}
