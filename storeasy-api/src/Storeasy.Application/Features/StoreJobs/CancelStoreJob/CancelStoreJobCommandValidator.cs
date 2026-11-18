using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.StoreJobs.CancelStoreJob;

/// <summary>
/// Validations of <see cref="CancelStoreJobCommand"/>.
/// </summary>
public sealed class CancelStoreJobCommandValidator : AbstractValidator<CancelStoreJobCommand>
{
    ///<inheritdoc cref="CancelStoreJobCommandValidator"/>
    public CancelStoreJobCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.JobId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreJob]]);
    }
}
