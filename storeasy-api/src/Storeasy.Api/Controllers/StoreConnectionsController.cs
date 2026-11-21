using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Features.StoreConnections.AddStoreConnection;
using Storeasy.Application.Features.StoreConnections.DeleteStoreConnection;
using Storeasy.Application.Features.StoreConnections.GetStoreConnectionList;
using Storeasy.Application.Features.StoreConnections.ReplaceStoreConnectionKey;
using Storeasy.Application.Features.StoreConnections.UpdateStoreConnection;
using Storeasy.Application.Features.StoreConnections.VerifyStoreConnection;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Store connection endpoints (App Store Connect API keys, Google Play service accounts). Secrets go in, never out.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class StoreConnectionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the organization's store connections (any member). Never the secret; the fingerprint and the last
    /// verification error only for owners and admins.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<List<StoreConnectionDto>>> GetConnectionsAsync([FromQuery] GetStoreConnectionListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds a store connection. The secret is validated offline, encrypted and never returned.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("connection")]
    [Idempotent]
    public Task<Response<int>> AddConnectionAsync(AddStoreConnectionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Renames a connection and sets or clears its icon (a generic icon or the connection's own store logo). The secret
    /// is not touched. Answers the updated connection.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("connection")]
    public Task<Response<StoreConnectionDto>> UpdateConnectionAsync(UpdateStoreConnectionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Replaces the key of an App Store Connect or Google Play connection in place (same connection id, so linked apps,
    /// jobs and history stay). An Apple key must have the connection's issuer id. The new key is verified against the
    /// store first; when that fails the old key stays. Answers the updated connection, never the secret.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("connection/key")]
    [RateLimit(20, 600, "store-key")]
    public Task<Response<StoreConnectionDto>> ReplaceKeyAsync(ReplaceStoreConnectionKeyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Checks a connection against its store and stores the verdict.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("connection/verify")]
    [RateLimit(20, 600, "store-verify")]
    public Task<Response<StoreConnectionDto>> VerifyConnectionAsync(VerifyStoreConnectionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a connection and wipes its secret.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("connection")]
    public Task<Response<int>> DeleteConnectionAsync(DeleteStoreConnectionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
