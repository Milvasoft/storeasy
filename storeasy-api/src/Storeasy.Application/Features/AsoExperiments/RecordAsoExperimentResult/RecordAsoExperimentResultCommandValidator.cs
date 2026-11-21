using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Features.AsoExperiments.RecordAsoExperimentResult;

/// <summary>
/// Validations of <see cref="RecordAsoExperimentResultCommand"/>.
/// </summary>
public sealed class RecordAsoExperimentResultCommandValidator : AbstractValidator<RecordAsoExperimentResultCommand>
{
    ///<inheritdoc cref="RecordAsoExperimentResultCommandValidator"/>
    public RecordAsoExperimentResultCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);

        RuleFor(x => x.ResultSummary)
            .MaximumLength(AsoExperiment.TextMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalAsoExperiment]]);

        RuleFor(x => x.ResultUrl)
            .Must(u => string.IsNullOrWhiteSpace(u) || (u.Length <= AsoExperiment.UrlMaxLength && Uri.TryCreate(u.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, "URL"]);
    }
}
