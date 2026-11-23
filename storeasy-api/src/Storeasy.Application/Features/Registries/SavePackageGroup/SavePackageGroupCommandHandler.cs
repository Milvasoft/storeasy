using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.SavePackageGroup;

/// <summary>
/// Handles <see cref="SavePackageGroupCommand"/>. A group or package of another organization is reported as not found.
/// </summary>
/// <param name="GroupRepository"></param>
/// <param name="PackageRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record SavePackageGroupCommandHandler(IStoreasyRepositoryBase<PackageGroup> GroupRepository, IStoreasyRepositoryBase<TrackedPackage> PackageRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SavePackageGroupCommand, int>
{
    private readonly IStoreasyRepositoryBase<PackageGroup> _groupRepository = GroupRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SavePackageGroupCommand request, CancellationToken cancellationToken)
    {
        var ids = request.TrackedPackageIds.Distinct().ToList();
        var found = await _packageRepository.GetAllAsync(p => ids.Contains(p.Id) && p.OrganizationId == request.OrganizationId, projection: p => new TrackedPackage { Id = p.Id }, cancellationToken: cancellationToken);

        if (found.Count != ids.Count)
            return Response<int>.Error(0, MessageKey.TrackedPackageNotFound);

        var group = request.Id is int id
            ? await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == id && g.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken)
            : new PackageGroup { OrganizationId = request.OrganizationId };

        if (group == null)
            return Response<int>.Error(0, MessageKey.PackageGroupNotFound);

        group.Name = request.Name.Trim();

        try
        {
            if (request.Id == null)
                await _groupRepository.AddAsync(group, cancellationToken);
            else
                await _groupRepository.UpdateAsync(group, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Response<int>.Error(0, MessageKey.PackageGroupNameTaken);
        }

        await _packageRepository.ExecuteUpdateAsync(p => p.OrganizationId == request.OrganizationId && p.PackageGroupId == group.Id && !ids.Contains(p.Id),
                                                    new SetPropertyBuilder<TrackedPackage>().SetPropertyValue(p => p.PackageGroupId, (int?)null),
                                                    cancellationToken);
        await _packageRepository.ExecuteUpdateAsync(p => p.OrganizationId == request.OrganizationId && ids.Contains(p.Id),
                                                    new SetPropertyBuilder<TrackedPackage>().SetPropertyValue(p => p.PackageGroupId, (int?)group.Id),
                                                    cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryGroupSaved, group.Id.ToString(CultureInfo.InvariantCulture), $"Package group {group.Name} saved with {ids.Count} packages.", details: new { group.Name, TrackedPackageIds = ids }, cancellationToken: cancellationToken);

        return Response<int>.Success(group.Id);
    }
}
