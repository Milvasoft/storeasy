using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.RemovePackageGroup;

/// <summary>
/// Handles <see cref="RemovePackageGroupCommand"/>. A group of another organization is reported as not found.
/// </summary>
/// <param name="GroupRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record RemovePackageGroupCommandHandler(IStoreasyRepositoryBase<PackageGroup> GroupRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<RemovePackageGroupCommand, int>
{
    private readonly IStoreasyRepositoryBase<PackageGroup> _groupRepository = GroupRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemovePackageGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == request.Id && g.OrganizationId == request.OrganizationId,
                                                                  projection: g => new PackageGroup { Id = g.Id, Name = g.Name },
                                                                  cancellationToken: cancellationToken);

        if (group == null)
            return Response<int>.Error(0, MessageKey.PackageGroupNotFound);

        await _groupRepository.ExecuteDeleteAsync(g => g.Id == group.Id && g.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryGroupRemoved, group.Id.ToString(CultureInfo.InvariantCulture), $"Package group {group.Name} removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(group.Id);
    }
}
