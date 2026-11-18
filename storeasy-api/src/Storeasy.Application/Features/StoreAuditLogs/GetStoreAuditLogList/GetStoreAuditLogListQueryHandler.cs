using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreAuditLogs.GetStoreAuditLogList;

/// <summary>
/// Handles <see cref="GetStoreAuditLogListQuery"/>.
/// </summary>
/// <param name="auditRepository"></param>
public class GetStoreAuditLogListQueryHandler(IStoreasyRepositoryBase<StoreAuditLog> auditRepository) : IInterceptable, IListQueryHandler<GetStoreAuditLogListQuery, StoreAuditLogDto>
{
    private readonly IStoreasyRepositoryBase<StoreAuditLog> _auditRepository = auditRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<StoreAuditLogDto>> Handle(GetStoreAuditLogListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(StoreAuditLogDto.Date), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var response = await _auditRepository.GetAllAsync(request,
                                                          condition: a => a.OrganizationId == request.OrganizationId,
                                                          projection: StoreAuditLogDto.Projection,
                                                          cancellationToken: cancellationToken);

        return response;
    }
}
