using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Features.Assets.CompleteAssetUpload;
using Storeasy.Application.Features.Assets.CreateAssetUpload;
using Storeasy.Application.Features.Assets.GetAssetDownloadUrl;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// File endpoints: the organization's files live in a private bucket and move only through presigned URLs, so the
/// bytes never pass through the API.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AssetsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Declares a file and returns presigned upload URLs: a single PUT, or one PUT per part for files over 64 MB
    /// (editors and above). 503 when no bucket is configured.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("upload")]
    [RateLimit(300, 3600, "asset-upload")]
    public Task<Response<AssetUploadDto>> CreateUploadAsync(CreateAssetUploadCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Confirms an upload (completes a multipart upload with the parts' ETags) after checking the stored size (editors
    /// and above). Retry safe: a confirmed asset is returned as is.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("asset/complete")]
    public Task<Response<AssetDto>> CompleteUploadAsync(CompleteAssetUploadCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Returns a short lived presigned download URL of a confirmed file (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("asset/download-url")]
    public Task<Response<AssetDownloadDto>> GetDownloadUrlAsync([FromQuery] GetAssetDownloadUrlQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
