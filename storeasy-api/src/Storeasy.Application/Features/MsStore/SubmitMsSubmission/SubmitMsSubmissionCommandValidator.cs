using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.SubmitMsSubmission;

/// <summary>
/// Validations of <see cref="SubmitMsSubmissionCommand"/>.
/// </summary>
public sealed class SubmitMsSubmissionCommandValidator : AbstractValidator<SubmitMsSubmissionCommand>
{
    ///<inheritdoc cref="SubmitMsSubmissionCommandValidator"/>
    public SubmitMsSubmissionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(command => command.Target)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer["Global.MsSubmission"]]);

        RuleFor(command => command.FlightId)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.MsFlightNotFound])
            .When(command => command.Target == MsSubmissionTarget.Flight);

        RuleFor(command => command.AddonStoreId)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.MsAddonNotFound])
            .When(command => command.Target == MsSubmissionTarget.Addon);

        RuleFor(command => command.Confirm)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.MsConfirmationMismatch]);
    }
}
