using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.LanguageDtos;
using Storeasy.Application.Features.Languages.GetLanguageList;
using Storeasy.Application.Features.Languages.UpdateLanguage;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Language endpoints.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
[UserTypeAuth(UserType.Manager)]
public class LanguagesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Gets languages.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.LanguageManagement.List)]
    [HttpPatch]
    public Task<ListResponse<LanguageDto>> GetLanguagesAsync(GetLanguageListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Updates language. Only the fields that are sent as isUpdated true are updated.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.LanguageManagement.Update)]
    [HttpPut("language")]
    public Task<Response<int>> UpdateLanguagesAsync(UpdateLanguageCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}