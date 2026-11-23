using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.SaveReleaseSettings;

/// <summary>
/// Handles <see cref="SaveReleaseSettingsCommand"/>. A package or group of another organization is reported as not found.
/// </summary>
/// <param name="PackageRepository"></param>
/// <param name="GroupRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record SaveReleaseSettingsCommandHandler(IStoreasyRepositoryBase<TrackedPackage> PackageRepository,
                                                IStoreasyRepositoryBase<PackageGroup> GroupRepository,
                                                IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SaveReleaseSettingsCommand, int>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IStoreasyRepositoryBase<PackageGroup> _groupRepository = GroupRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SaveReleaseSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = Normalize(request.Settings);

        if (settings != null && ReleaseRules.Problems(settings) is { Count: > 0 } problems)
            return Response<int>.Error(0, problems[0]);

        int affected;

        if (request.TrackedPackageId is int packageId)
        {
            affected = await _packageRepository.ExecuteUpdateAsync(p => p.Id == packageId && p.OrganizationId == request.OrganizationId,
                                                                   new SetPropertyBuilder<TrackedPackage>().SetPropertyValue(p => p.Release, settings),
                                                                   cancellationToken);

            if (affected == 0)
                return Response<int>.Error(0, MessageKey.TrackedPackageNotFound);
        }
        else
        {
            affected = await _groupRepository.ExecuteUpdateAsync(g => g.Id == request.PackageGroupId && g.OrganizationId == request.OrganizationId,
                                                                 new SetPropertyBuilder<PackageGroup>().SetPropertyValue(g => g.Release, settings),
                                                                 cancellationToken);

            if (affected == 0)
                return Response<int>.Error(0, MessageKey.PackageGroupNotFound);
        }

        var target = (request.TrackedPackageId ?? request.PackageGroupId).Value.ToString(CultureInfo.InvariantCulture);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryReleaseSettingsSaved, target, settings == null ? "Release settings cleared." : $"Release settings: {settings.Repository} ({settings.Trigger}).", details: new { request.TrackedPackageId, request.PackageGroupId, Settings = settings }, cancellationToken: cancellationToken);

        return Response<int>.Success(affected);
    }

    private static ReleaseSettings Normalize(ReleaseSettings settings)
    {
        if (settings == null)
            return null;

        return new ReleaseSettings
        {
            Repository = settings.Repository?.Trim(),
            Branch = string.IsNullOrWhiteSpace(settings.Branch) ? "master" : settings.Branch.Trim(),
            Trigger = settings.Trigger,
            VersionFiles = settings.Trigger is ReleaseTrigger.VersionBump or ReleaseTrigger.VersionBumpAndTag
                ? [.. (settings.VersionFiles ?? []).Select(f => new VersionFileSetting { Path = f.Path?.Trim().TrimStart('/'), Format = f.Format })]
                : [],
            TagPattern = settings.Trigger is ReleaseTrigger.Tag or ReleaseTrigger.VersionBumpAndTag ? settings.TagPattern?.Trim() : null,
            Workflow = string.IsNullOrWhiteSpace(settings.Workflow) ? null : settings.Workflow.Trim(),
            DispatchVersionInput = settings.Trigger == ReleaseTrigger.Dispatch ? settings.DispatchVersionInput?.Trim() : null,
            DispatchPrereleaseInput = settings.Trigger == ReleaseTrigger.Dispatch && !string.IsNullOrWhiteSpace(settings.DispatchPrereleaseInput) ? settings.DispatchPrereleaseInput.Trim() : null,
            DispatchTakesTag = settings.Trigger == ReleaseTrigger.Dispatch && settings.DispatchTakesTag,
        };
    }
}
