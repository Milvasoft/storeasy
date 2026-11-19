using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.RemoveOrganizationLogo;

/// <summary>
/// Handles <see cref="RemoveOrganizationLogoCommand"/>: clears the logo columns, deletes the stored file and writes an
/// audit entry. Without a logo nothing is written.
/// </summary>
/// <param name="OrganizationRepository"></param>
/// <param name="FileStorage"></param>
/// <param name="AuditTrail"></param>
[Log]
public record RemoveOrganizationLogoCommandHandler(IStoreasyRepositoryBase<Organization> OrganizationRepository,
                                                   IFileStorage FileStorage,
                                                   IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<RemoveOrganizationLogoCommand, int>
{
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly IFileStorage _fileStorage = FileStorage;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemoveOrganizationLogoCommand request, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetFirstOrDefaultAsync(o => o.Id == request.OrganizationId,
                                                                                projection: o => new { o.LogoKey },
                                                                                cancellationToken: cancellationToken);

        if (organization == null)
            return Response<int>.Error(0, MessageKey.OrganizationNotFound);

        if (string.IsNullOrEmpty(organization.LogoKey))
            return Response<int>.Success(request.OrganizationId);

        var builder = new SetPropertyBuilder<Organization>().SetPropertyValue(o => o.LogoKey, null)
                                                            .SetPropertyValue(o => o.LogoUpdateDate, null);

        await _organizationRepository.ExecuteUpdateAsync(o => o.Id == request.OrganizationId, builder, cancellationToken);

        if (_fileStorage.IsConfigured)
            await _fileStorage.DeleteAsync(organization.LogoKey, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.OrganizationLogoRemoved,
                                     request.OrganizationId.ToString(),
                                     "Organization logo removed.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(request.OrganizationId);
    }
}
