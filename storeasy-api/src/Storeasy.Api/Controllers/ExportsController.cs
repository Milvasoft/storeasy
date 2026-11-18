using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ExportDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain;
using System.Net;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Export endpoints.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ExportsController(IExportService exportService) : ControllerBase
{
    private readonly IExportService _exportService = exportService;

    /// <summary>
    /// Dynamically creates an excel file according to the export type. Answers 503 while the
    /// <see cref="FeatureFlagKeys.Export"/> switch is off.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPost("export")]
    [FeatureFlag(FeatureFlagKeys.Export)]
    [ProducesDefaultResponseType(typeof(FileResult))]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(NotFoundResult), 400)]
    public async Task<IActionResult> ExportProductsToExcelAsync(ExportRequest request, CancellationToken cancellation)
    {
        var fileResponse = await _exportService.DynamicExportToExcelAsync(request, cancellation);

        if (fileResponse.Data?.FileStream == null)
        {
            fileResponse.Data = null;
            return Ok(fileResponse);
        }

        Response.ContentType = fileResponse.Data.MimeType;
        Response.StatusCode = (int)HttpStatusCode.OK;

        return File(fileResponse.Data.FileStream, fileResponse.Data.MimeType, fileResponse.Data.FileName);
    }
}