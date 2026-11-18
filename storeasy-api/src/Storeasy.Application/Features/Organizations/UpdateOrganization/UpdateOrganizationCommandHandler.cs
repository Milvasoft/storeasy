using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Utils.Helpers;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.UpdateOrganization;

/// <summary>
/// Handles <see cref="UpdateOrganizationCommand"/>. A slug used by any organization (a deleted one included, the unique
/// index covers every row) is a 409; a concurrent change to the same slug loses on the unique index and gets the same.
/// </summary>
/// <param name="OrganizationRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateOrganizationCommandHandler(IStoreasyRepositoryBase<Organization> OrganizationRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateOrganizationCommand, int>
{
    private const string _slugIndexName = "IX_Organizations_Slug";

    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : OrgRoleRules.ToSlug(request.Slug);
        var builder = new SetPropertyBuilder<Organization>().SetPropertyValue(o => o.Name, name);

        if (slug != null)
        {
            _organizationRepository.FetchSoftDeletedEntities(true);

            var slugTaken = await _organizationRepository.GetFirstOrDefaultAsync(o => o.Slug == slug && o.Id != request.OrganizationId, projection: o => (int?)o.Id, cancellationToken: cancellationToken);

            if (slugTaken.HasValue)
                return Response<int>.Error(0, MessageKey.OrganizationSlugTaken);

            builder = builder.SetPropertyValue(o => o.Slug, slug);
        }

        int updatedCount;

        try
        {
            updatedCount = await _organizationRepository.ExecuteUpdateAsync(request.OrganizationId, builder, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (UserUniqueConstraintHelper.IsUniqueViolation(ex, _slugIndexName))
        {
            return Response<int>.Error(0, MessageKey.OrganizationSlugTaken);
        }

        if (updatedCount == 0)
            return Response<int>.Error(0, MessageKey.OrganizationNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.OrganizationUpdated,
                                     request.OrganizationId.ToString(),
                                     slug == null ? $"Organization renamed to '{name}'." : $"Organization renamed to '{name}', slug '{slug}'.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(request.OrganizationId);
    }
}
