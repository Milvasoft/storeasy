using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Features.AsoExperiments.SaveAsoExperiment;

/// <summary>
/// Validations of <see cref="SaveAsoExperimentCommand"/> (the variants are checked by the handler against the kind).
/// </summary>
public sealed class SaveAsoExperimentCommandValidator : AbstractValidator<SaveAsoExperimentCommand>
{
    ///<inheritdoc cref="SaveAsoExperimentCommandValidator"/>
    public SaveAsoExperimentCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .When(x => x.Id == null)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Kind)
            .IsInEnum()
            .When(x => x.Id == null)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);

        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= AsoExperiment.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(x => x.Note)
            .MaximumLength(AsoExperiment.TextMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);
    }
}
