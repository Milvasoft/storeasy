using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.GetAssistedStepDetail;

/// <summary>
/// Validations of <see cref="GetAssistedStepDetailQuery"/>.
/// </summary>
public sealed class GetAssistedStepDetailQueryValidator : AbstractValidator<GetAssistedStepDetailQuery>
{
    ///<inheritdoc cref="GetAssistedStepDetailQueryValidator"/>
    public GetAssistedStepDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StepId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAssistedStep]]);
    }
}
