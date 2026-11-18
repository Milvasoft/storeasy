using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.GetMyOrganizations;

/// <summary>
/// Handles <see cref="GetMyOrganizationsQuery"/>.
/// </summary>
/// <param name="memberRepository"></param>
/// <param name="currentUser"></param>
public class GetMyOrganizationsQueryHandler(IStoreasyRepositoryBase<OrganizationMember> memberRepository, ICurrentUser currentUser) : IInterceptable, IQueryHandler<GetMyOrganizationsQuery, List<MyOrganizationDto>>
{
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = memberRepository;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public async Task<Response<List<MyOrganizationDto>>> Handle(GetMyOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        var organizations = await _memberRepository.GetAllAsync(m => m.UserId == userId && !m.Organization.IsDeleted,
                                                                projection: MyOrganizationDto.Projection,
                                                                cancellationToken: cancellationToken);

        foreach (var organization in organizations)
            organization.Permissions = [.. OrgAccess.Of(organization.Role, organization.CustomRoleId, organization.CustomPermissions).Permissions.Order()];

        return Response<List<MyOrganizationDto>>.Success([.. organizations.OrderBy(o => o.Name)]);
    }
}
