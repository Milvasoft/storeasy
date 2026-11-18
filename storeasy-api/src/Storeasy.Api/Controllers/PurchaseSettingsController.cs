using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Features.PurchaseSettings.GetSandboxTesterList;
using Storeasy.Application.Features.PurchaseSettings.GetServerNotifications;
using Storeasy.Application.Features.PurchaseSettings.PlanSandboxTesters;
using Storeasy.Application.Features.PurchaseSettings.PlanServerNotifications;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Purchase settings of an app: App Store Server Notifications addresses (Play Real-time developer notifications are an
/// assisted step) and App Store sandbox testers.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class PurchaseSettingsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the server notification addresses live (Play: the assisted step). Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("server-notifications")]
    public Task<Response<ServerNotificationsDto>> GetServerNotificationsAsync([FromQuery] GetServerNotificationsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans the App Store Server Notifications addresses (dry run by default). Owner and Admin (ManageWebhooks).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("server-notifications/plan")]
    [RateLimit(60, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanServerNotificationsAsync(PlanServerNotificationsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the App Store sandbox testers of the developer account, live. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("sandbox-testers")]
    public Task<Response<List<SandboxTesterDto>>> GetSandboxTestersAsync([FromQuery] GetSandboxTesterListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans sandbox tester changes (storefront, interrupted purchases, renewal rate, purchase history). Editor and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sandbox-testers/plan")]
    [RateLimit(60, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanSandboxTestersAsync(PlanSandboxTestersCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
