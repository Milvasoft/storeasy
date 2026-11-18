using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Application.Features.Mockups.CreateMockupTemplate;
using Storeasy.Application.Features.Mockups.DeleteMockupTemplate;
using Storeasy.Application.Features.Mockups.ExportMockups;
using Storeasy.Application.Features.Mockups.GetMockupExportManifest;
using Storeasy.Application.Features.Mockups.GetMockupExportPlan;
using Storeasy.Application.Features.Mockups.GetMockupExportZip;
using Storeasy.Application.Features.Mockups.GetMockupFrames;
using Storeasy.Application.Features.Mockups.GetMockupStarterTemplates;
using Storeasy.Application.Features.Mockups.GetMockupTemplateDetail;
using Storeasy.Application.Features.Mockups.GetMockupTemplateList;
using Storeasy.Application.Features.Mockups.RenderMockupPreview;
using Storeasy.Application.Features.Mockups.SaveMockupCaptions;
using Storeasy.Application.Features.Mockups.SuggestMockupCaptions;
using Storeasy.Application.Features.Mockups.UpdateMockupTemplate;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain;
using Storeasy.Domain.JsonModels;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Mockup and screenshot studio (docs/urun-plani.md 2.4, design 12): templates per app, server side previews and exports
/// rendered from the template JSON with storeasy's own device frames and embedded open licensed fonts, caption
/// localization, and the export manifest that feeds the screenshot upload plan. Reading and previewing is open to every
/// member; writing and exporting needs an editor.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class MockupsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the organization's templates, optionally of one app (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("templates")]
    public Task<ListResponse<MockupTemplateListDto>> GetTemplatesAsync(GetMockupTemplateListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one template with its design, slots and captions (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("template")]
    public Task<Response<MockupTemplateDetailDto>> GetTemplateAsync([FromQuery] GetMockupTemplateDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Creates a template from a starter or as a copy of another template (editors and above). Retry safe with an
    /// <c>Idempotency-Key</c> header.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("template")]
    [Idempotent]
    public Task<Response<int>> CreateTemplateAsync(CreateMockupTemplateCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves a template's name, design, locales and slots (editors and above). 409 when someone saved since the given
    /// version. Returns the new version.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("template")]
    public Task<Response<int>> UpdateTemplateAsync(UpdateMockupTemplateCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Deletes a template (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("template")]
    public Task<Response<int>> DeleteTemplateAsync([FromQuery] DeleteMockupTemplateCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves caption cells and answers the captions that overflow in the narrowest exported class (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("captions")]
    public Task<Response<MockupCaptionsSavedDto>> SaveCaptionsAsync(SaveMockupCaptionsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Suggests captions in one locale (editors and above). Kill switch <c>Studio.AiCaptions</c>; 503 when it is off or no
    /// translator is configured. Nothing is saved.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("captions/suggest")]
    [FeatureFlag(FeatureFlagKeys.StudioAiCaptions)]
    [RateLimit(60, 3600, "mockup-caption-suggest")]
    public Task<Response<MockupCaptionSuggestionDto>> SuggestCaptionsAsync(SuggestMockupCaptionsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the device frames and the screenshot classes drawn with each (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("frames")]
    public Task<Response<List<MockupFrameDto>>> GetFramesAsync([FromQuery] GetMockupFramesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the built in starter designs (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("starter-templates")]
    public Task<Response<List<MockupStarterTemplateDto>>> GetStarterTemplatesAsync([FromQuery] GetMockupStarterTemplatesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Renders small previews of up to four slots in one locale and class, from the stored or an unsaved design (any
    /// member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("render-preview")]
    [RateLimit(1200, 3600, "mockup-preview")]
    public Task<Response<MockupPreviewDto>> RenderPreviewAsync(RenderMockupPreviewQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans an export: classes of the app's stores with their requirement, refused sizes and the image count (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("export/plan")]
    public Task<Response<MockupExportPlanDto>> GetExportPlanAsync(GetMockupExportPlanQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues the export job of the plan (editors and above); follow it on the activity hub. Retry safe with an
    /// <c>Idempotency-Key</c> header. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("export")]
    [Idempotent]
    [RateLimit(60, 3600, "mockup-export")]
    public Task<Response<long>> ExportAsync(ExportMockupsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the manifest of a finished export: asset ids by store, class and locale for the upload plan (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("export/manifest")]
    public Task<Response<MockupExportManifest>> GetExportManifestAsync([FromQuery] GetMockupExportManifestQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Returns a presigned download URL of the zip of a finished export (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("export/zip")]
    public Task<Response<AssetDownloadDto>> GetExportZipAsync([FromQuery] GetMockupExportZipQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
