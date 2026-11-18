using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.SaveMsFlightGroup;

/// <summary>
/// Handles <see cref="SaveMsFlightGroupCommand"/>: one row per organization and group id, updated in place.
/// </summary>
/// <param name="GroupRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record SaveMsFlightGroupCommandHandler(IStoreasyRepositoryBase<MsFlightGroup> GroupRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SaveMsFlightGroupCommand, int>
{
    private readonly IStoreasyRepositoryBase<MsFlightGroup> _groupRepository = GroupRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SaveMsFlightGroupCommand request, CancellationToken cancellationToken)
    {
        var groupId = request.GroupId.Trim();
        var name = request.Name.Trim();

        var id = await _groupRepository.GetFirstOrDefaultAsync(g => g.OrganizationId == request.OrganizationId && g.GroupId == groupId, projection: g => (int?)g.Id, cancellationToken: cancellationToken);

        if (id.HasValue)
        {
            await _groupRepository.ExecuteUpdateAsync(id.Value,
                                                      new SetPropertyBuilder<MsFlightGroup>().SetPropertyValue(g => g.Name, name).SetPropertyValue(g => g.MemberCount, request.MemberCount),
                                                      cancellationToken);
        }
        else
        {
            var group = new MsFlightGroup { OrganizationId = request.OrganizationId, GroupId = groupId, Name = name, MemberCount = request.MemberCount };

            try
            {
                await _groupRepository.AddAsync(group, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent save of the same group won the unique index; that save stands.
                return Response<int>.Error(0, MessageKey.PostgreDuplicateDataException);
            }

            id = group.Id;
        }

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.MsFlightGroupSaved, groupId, $"Flight group {groupId} named '{name}'.", StoreKind.Microsoft, cancellationToken: cancellationToken);

        return Response<int>.Success(id.Value);
    }
}
