using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Features.Notifications.GetNotificationPreferences;
using Storeasy.Application.Features.Notifications.UpdateNotificationPreference;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// The caller's notification preferences (<c>account/preferences</c>): channels per notification kind, valid in every
/// organization. Web push is sent to the browsers the caller subscribed (when the instance has a VAPID key pair).
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/account")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AccountPreferencesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The caller's channels per notification kind.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpGet("preferences")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<List<NotificationPreferenceDto>>> GetPreferencesAsync(CancellationToken cancellation) => _mediator.Send(new GetNotificationPreferencesQuery(), cancellation);

    /// <summary>
    /// Saves the caller's channels for one kind.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPut("preferences")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<NotificationPreferenceDto>> UpdatePreferenceAsync(UpdateNotificationPreferenceCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
