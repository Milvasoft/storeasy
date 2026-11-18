using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetExpiringMsConnectionList;

/// <summary>
/// Handles <see cref="GetExpiringMsConnectionListQuery"/> through the shared expiry reader, limited to the organization.
/// </summary>
/// <param name="expiryReader"></param>
/// <param name="timeProvider"></param>
public class GetExpiringMsConnectionListQueryHandler(IMsConnectionExpiryReader expiryReader, TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetExpiringMsConnectionListQuery, List<MsExpiringConnectionDto>>
{
    private readonly IMsConnectionExpiryReader _expiryReader = expiryReader;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<MsExpiringConnectionDto>>> Handle(GetExpiringMsConnectionListQuery request, CancellationToken cancellationToken)
    {
        var until = _timeProvider.GetUtcNow().UtcDateTime.AddDays(request.Days);

        return Response<List<MsExpiringConnectionDto>>.Success(await _expiryReader.GetExpiringAsync(until, request.OrganizationId, cancellationToken));
    }
}
