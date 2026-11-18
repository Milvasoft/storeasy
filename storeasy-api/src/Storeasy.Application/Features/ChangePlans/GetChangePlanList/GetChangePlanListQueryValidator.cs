using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Domain.Content;

namespace Storeasy.Application.Features.ChangePlans.GetChangePlanList;

/// <summary>
/// Validations of <see cref="GetChangePlanListQuery"/>.
/// </summary>
public sealed class GetChangePlanListQueryValidator : AbstractValidator<GetChangePlanListQuery>
{
    ///<inheritdoc cref="GetChangePlanListQueryValidator"/>
    public GetChangePlanListQueryValidator(IMilvaLocalizer localizer)
    {
    }
}
