using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentOverview;

/// <summary>
/// Validations of <see cref="GetAsoExperimentOverviewQuery"/>.
/// </summary>
public sealed class GetAsoExperimentOverviewQueryValidator : AbstractValidator<GetAsoExperimentOverviewQuery>
{
    ///<inheritdoc cref="GetAsoExperimentOverviewQueryValidator"/>
    public GetAsoExperimentOverviewQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
