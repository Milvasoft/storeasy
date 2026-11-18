using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.SettingsDtos;

namespace Storeasy.Application.Features.Settings.GetSystemStatus;

/// <summary>
/// Handles <see cref="GetSystemStatusQuery"/>.
/// </summary>
/// <param name="statusProbe"></param>
public class GetSystemStatusQueryHandler(ISystemStatusProbe statusProbe) : IInterceptable, IQueryHandler<GetSystemStatusQuery, SystemStatusDto>
{
    private readonly ISystemStatusProbe _statusProbe = statusProbe;

    /// <inheritdoc/>
    public async Task<Response<SystemStatusDto>> Handle(GetSystemStatusQuery request, CancellationToken cancellationToken)
        => Response<SystemStatusDto>.Success(await _statusProbe.CheckAsync(cancellationToken));
}
