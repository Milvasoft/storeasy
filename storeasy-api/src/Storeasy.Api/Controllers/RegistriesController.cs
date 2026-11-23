using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Features.Registries.AddTrackedPackage;
using Storeasy.Application.Features.Registries.GetPackageAlertList;
using Storeasy.Application.Features.Registries.GetPackageDownloads;
using Storeasy.Application.Features.Registries.GetPackageListing;
using Storeasy.Application.Features.Registries.GetPackageGroupList;
using Storeasy.Application.Features.Registries.GetPackageGroupSuggestions;
using Storeasy.Application.Features.Registries.SavePackageGroup;
using Storeasy.Application.Features.Registries.RemovePackageGroup;
using Storeasy.Application.Features.Registries.GetPackageSetup;
using Storeasy.Application.Features.Registries.GetTokenReminderList;
using Storeasy.Application.Features.Registries.RemoveTokenReminder;
using Storeasy.Application.Features.Registries.SavePackageSetup;
using Storeasy.Application.Features.Registries.SaveTokenReminder;
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

    /// <summary>
    /// The trusted publishing wizard of a package: registry settings, the CI file to copy and the check of the latest
    /// release (the saved setup, or a preview of the given values).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("setup")]
    public Task<Response<TrustedPublishingDto>> GetSetupAsync([FromQuery] GetPackageSetupQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves or clears the trusted publishing setup of a package.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("setup")]
    public Task<Response<TrustedPublishingDto>> SaveSetupAsync(SavePackageSetupCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The registry token reminders of the organization (never the tokens).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("reminders")]
    public Task<Response<List<TokenReminderDto>>> GetRemindersAsync([FromQuery] GetTokenReminderListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds or changes a token reminder; a value that looks like a token is refused.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("reminder")]
    [RateLimit(60, 3600, "registries-reminder-save")]
    public Task<Response<int>> SaveReminderAsync(SaveTokenReminderCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a token reminder.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("reminder")]
    public Task<Response<int>> RemoveReminderAsync(RemoveTokenReminderCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Downloads and usage of a package: totals, daily series, per version counts.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("downloads")]
    public Task<Response<PackageDownloadsDto>> GetDownloadsAsync([FromQuery] GetPackageDownloadsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The package's registry listing, read live, with readiness checks.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("listing")]
    [RateLimit(300, 3600, "registries-listing")]
    public Task<Response<PackageListingDto>> GetListingAsync([FromQuery] GetPackageListingQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The package groups with their members.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("groups")]
    public Task<Response<List<PackageGroupDto>>> GetGroupsAsync([FromQuery] GetPackageGroupListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Suggested groups of packages that share a source repository.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("groups/suggestions")]
    public Task<Response<List<PackageGroupSuggestionDto>>> GetGroupSuggestionsAsync([FromQuery] GetPackageGroupSuggestionsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds or changes a package group.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("group")]
    public Task<Response<int>> SaveGroupAsync(SavePackageGroupCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a package group (its packages stay tracked).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("group")]
    public Task<Response<int>> RemoveGroupAsync(RemovePackageGroupCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
