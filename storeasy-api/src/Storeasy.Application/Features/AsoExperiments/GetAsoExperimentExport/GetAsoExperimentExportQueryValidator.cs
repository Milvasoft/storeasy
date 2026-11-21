using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentExport;

/// <summary>
/// Validations of <see cref="GetAsoExperimentExportQuery"/>.
/// </summary>
public sealed class GetAsoExperimentExportQueryValidator : AbstractValidator<GetAsoExperimentExportQuery>
{
    ///<inheritdoc cref="GetAsoExperimentExportQueryValidator"/>
    public GetAsoExperimentExportQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);
    }
}
