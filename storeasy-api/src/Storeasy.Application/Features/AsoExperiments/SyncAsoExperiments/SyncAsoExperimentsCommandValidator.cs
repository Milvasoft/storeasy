using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoExperiments.SyncAsoExperiments;

/// <summary>
/// Validations of <see cref="SyncAsoExperimentsCommand"/>.
/// </summary>
public sealed class SyncAsoExperimentsCommandValidator : AbstractValidator<SyncAsoExperimentsCommand>
{
    ///<inheritdoc cref="SyncAsoExperimentsCommandValidator"/>
    public SyncAsoExperimentsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
