using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.GetAssistedStepList;

/// <summary>
/// Validations of <see cref="GetAssistedStepListQuery"/>.
/// </summary>
public sealed class GetAssistedStepListQueryValidator : AbstractValidator<GetAssistedStepListQuery>
{
    ///<inheritdoc cref="GetAssistedStepListQueryValidator"/>
    public GetAssistedStepListQueryValidator(IMilvaLocalizer localizer)
    {
    }
}
