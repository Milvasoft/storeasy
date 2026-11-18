using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Features.MsStore.AddMsProduct;
using Storeasy.Application.Features.MsStore.ApplyMsChange;
using Storeasy.Application.Features.MsStore.CheckMsInstaller;
using Storeasy.Application.Features.MsStore.GetExpiringMsConnectionList;
using Storeasy.Application.Features.MsStore.GetMsAddonList;
using Storeasy.Application.Features.MsStore.GetMsContent;
using Storeasy.Application.Features.MsStore.GetMsFlightList;
using Storeasy.Application.Features.MsStore.GetMsInstallerPackageList;
using Storeasy.Application.Features.MsStore.GetMsProductDetail;
using Storeasy.Application.Features.MsStore.GetMsSubmissionList;
using Storeasy.Application.Features.MsStore.RotateMsConnectionSecret;
using Storeasy.Application.Features.MsStore.SaveMsFlightGroup;
using Storeasy.Application.Features.MsStore.SubmitMsSubmission;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Microsoft Store endpoints (route prefix <c>ms</c>): MSIX apps and MSI/EXE products, submissions and certification
/// status, package flights, add-ons, installer packages, the plan and apply flow of every write, submitting, and the Entra
/// connection's secret expiry. Reads of storeasy's rows never call Microsoft; <c>ms/content</c> and <c>ms/change</c>
/// read the store in the request; every write runs as a store job.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class MsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Adds an MSI/EXE product by its Store id (editors and above). A sync job confirms it; follow the returned job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("product")]
    [RateLimit(60, 3600, "ms-product-add")]
    public Task<Response<MsProductAddedDto>> AddProductAsync(AddMsProductCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads a Microsoft Store product with its active submission, the Partner Center rules and its assisted steps (any
    /// member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("product")]
    public Task<Response<MsProductDto>> GetProductAsync([FromQuery] GetMsProductDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the submissions of a product with status history and the "changed in Partner Center" flag (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("submissions")]
    public Task<Response<List<MsSubmissionDto>>> GetSubmissionsAsync([FromQuery] GetMsSubmissionListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the package flights of an MSIX app with their group names (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("flights")]
    public Task<Response<List<MsFlightDto>>> GetFlightsAsync([FromQuery] GetMsFlightListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the add-ons of an MSIX app; Partner Center only ones are marked (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("addons")]
    public Task<Response<List<MsAddonDto>>> GetAddonsAsync([FromQuery] GetMsAddonListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the installer packages of an MSI/EXE product with their Authenticode check (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("packages")]
    public Task<Response<List<MsInstallerPackageDto>>> GetPackagesAsync([FromQuery] GetMsInstallerPackageListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the current store content of a product, flight or add-on from Microsoft (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("content")]
    [RateLimit(120, 3600, "ms-content")]
    public Task<Response<MsContentDto>> GetContentAsync([FromQuery] GetMsContentQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans a change (DryRun true, the default) or applies it with the plan's BaseHash (editors and above; rollout of an
    /// app submission needs ReleaseProduction, flights ReleaseTest). Applying queues a store job; drafts are sent with
    /// <c>ms/submission/submit</c>.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("change")]
    [RateLimit(120, 3600, "ms-change")]
    public Task<Response<MsChangePlanDto>> ApplyChangeAsync(ApplyMsChangeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sends a draft to certification (owners and admins: SubmitForReview), with the product id typed as confirmation.
    /// Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("submission/submit")]
    [RateLimit(20, 3600, "ms-submit")]
    public Task<Response<long>> SubmitAsync(SubmitMsSubmissionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues the Authenticode check of an MSI/EXE product's installers (editors and above). Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("packages/verify")]
    [RateLimit(30, 3600, "ms-packages-verify")]
    public Task<Response<long>> VerifyPackagesAsync(CheckMsInstallerCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Records the name of a Partner Center flight group (ReleaseTest).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("flight-group")]
    public Task<Response<int>> SaveFlightGroupAsync(SaveMsFlightGroupCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Replaces the client secret of a Microsoft connection after verifying the new one (owners and admins).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("connection/secret")]
    [RateLimit(20, 600, "ms-connection-secret")]
    public Task<Response<StoreConnectionDto>> RotateSecretAsync(RotateMsConnectionSecretCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the Microsoft connections whose client secret expires within the given days (owners and admins).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("connections/expiring")]
    public Task<Response<List<MsExpiringConnectionDto>>> GetExpiringConnectionsAsync([FromQuery] GetExpiringMsConnectionListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
