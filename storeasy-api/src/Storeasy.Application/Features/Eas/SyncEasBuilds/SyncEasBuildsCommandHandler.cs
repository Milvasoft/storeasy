using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SyncEasBuilds;

/// <summary>
/// Handles <see cref="SyncEasBuildsCommand"/>.
/// </summary>
/// <param name="EasService"></param>
public record SyncEasBuildsCommandHandler(IEasService EasService) : IInterceptable, ICommandHandler<SyncEasBuildsCommand, long>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<long>> Handle(SyncEasBuildsCommand request, CancellationToken cancellationToken)
        => _easService.QueueSyncAsync(request.OrganizationId, request.AppId, cancellationToken);
}
