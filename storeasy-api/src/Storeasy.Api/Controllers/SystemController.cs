using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.FeatureFlagDtos;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Features.FeatureFlags.GetFeatureFlagStates;
using Storeasy.Application.Features.Settings.GetPublicSettings;
using Storeasy.Application.Features.Settings.GetSystemSettings;
using Storeasy.Application.Features.Settings.GetSystemStatus;
using Storeasy.Application.Features.Settings.UpdateRegistrationMode;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Instance settings and status: the anonymous public settings, the feature flag states every user reads, and the status
/// and settings of platform managers (<see cref="PermissionCatalog.SystemManagement"/>).
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class SystemController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Registration mode, whether Google sign in is on and the web push public key (anonymous).
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("public-settings")]
    [AllowAnonymous]
    [RateLimit(120, 60, "system-public-settings")]
    public Task<Response<PublicSettingsDto>> GetPublicSettingsAsync(CancellationToken cancellation) => _mediator.Send(new GetPublicSettingsQuery(), cancellation);

    /// <summary>
    /// Whether each feature is on (read only, every signed in user; managing the flags is <c>featureFlags</c>).
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpGet("feature-flags")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<List<FeatureFlagStateDto>>> GetFeatureFlagStatesAsync(CancellationToken cancellation) => _mediator.Send(new GetFeatureFlagStatesQuery(), cancellation);

    /// <summary>
    /// Instance status for platform managers: build, database, Redis, object storage, mail provider, live event
    /// backplane, whether the encryption key and the optional features are configured (yes or no only) and the job queues
    /// across organizations.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.SystemManagement.Status)]
    [HttpGet("status")]
    [UserTypeAuth(UserType.Manager)]
    public Task<Response<SystemStatusDto>> GetStatusAsync(CancellationToken cancellation) => _mediator.Send(new GetSystemStatusQuery(), cancellation);

    /// <summary>
    /// Instance settings a platform manager may change: the registration mode in force and where it comes from.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.SystemManagement.Settings)]
    [HttpGet("settings")]
    [UserTypeAuth(UserType.Manager)]
    public Task<Response<SystemSettingsDto>> GetSettingsAsync(CancellationToken cancellation) => _mediator.Send(new GetSystemSettingsQuery(), cancellation);

    /// <summary>
    /// Saves the registration mode (overrides <c>Storeasy:Registration:Mode</c>); null returns to the configured one.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.SystemManagement.Settings)]
    [HttpPut("settings/registration-mode")]
    [UserTypeAuth(UserType.Manager)]
    public Task<Response<RegistrationMode>> UpdateRegistrationModeAsync(UpdateRegistrationModeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
