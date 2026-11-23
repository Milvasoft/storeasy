using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Features.Registries.AddTrackedPackage;
using Storeasy.Application.Features.Registries.GetPackageAlertList;
using Storeasy.Application.Features.Registries.GetTrackedPackage;
using Storeasy.Application.Features.Registries.GetTrackedPackageList;
using Storeasy.Application.Features.Registries.RemoveTrackedPackage;
using Storeasy.Application.Features.Registries.SyncTrackedPackage;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Package registries module (npm, NuGet, PyPI, crates.io): tracked packages, their versions and alerts, read from the
/// registries' public APIs and OSV.dev. storeasy watches packages and never publishes them; no registry token is stored.
/// The whole module answers 503 while the <c>Registries</c> feature flag is off.
/// </summary>
/// <param name="mediator"></param>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
[FeatureFlag(FeatureFlagKeys.Registries)]
public class RegistriesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The package summary: every tracked package with its latest version, publish date, CI run, provenance and open alerts.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("packages")]
    public Task<Response<List<TrackedPackageSummaryDto>>> GetPackagesAsync([FromQuery] GetTrackedPackageListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// One tracked package with its versions and alerts.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("package")]
    public Task<Response<TrackedPackageDto>> GetPackageAsync([FromQuery] GetTrackedPackageQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The package alerts of the organization (open ones unless resolved ones are asked for).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("alerts")]
    public Task<Response<List<PackageAlertDto>>> GetAlertsAsync([FromQuery] GetPackageAlertListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Starts tracking a package (the name is checked on the registry) and queues its first sync.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("package")]
    [RateLimit(60, 3600, "registries-package-add")]
    public Task<Response<TrackedPackageAddedDto>> AddPackageAsync(AddTrackedPackageCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Stops tracking a package.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("package")]
    public Task<Response<int>> RemovePackageAsync(RemoveTrackedPackageCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of one package, or of every package.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "registries-sync")]
    public Task<Response<long>> SyncAsync(SyncTrackedPackageCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
