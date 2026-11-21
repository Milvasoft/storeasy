using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentDetail;

/// <summary>
/// Validations of <see cref="GetAsoExperimentDetailQuery"/>.
/// </summary>
public sealed class GetAsoExperimentDetailQueryValidator : AbstractValidator<GetAsoExperimentDetailQuery>
{
    ///<inheritdoc cref="GetAsoExperimentDetailQueryValidator"/>
    public GetAsoExperimentDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);
    }
}
