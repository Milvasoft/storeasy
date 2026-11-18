using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroupTesters;

/// <summary>
/// Validations of <see cref="UpdateBetaGroupTestersCommand"/>.
/// </summary>
public sealed class UpdateBetaGroupTestersCommandValidator : AbstractValidator<UpdateBetaGroupTestersCommand>
{
    ///<inheritdoc cref="UpdateBetaGroupTestersCommandValidator"/>
    public UpdateBetaGroupTestersCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command)
            .Must(command => (command.Add?.Count ?? 0) + (command.Remove?.Count ?? 0) is > 0 and <= 500)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBetaTester]]);

        RuleForEach(command => command.Add)
            .Must(t => t != null && !string.IsNullOrWhiteSpace(t.Email) && t.Email.Length <= 200 && t.Email.Contains('@') && (t.FirstName?.Length ?? 0) <= 200 && (t.LastName?.Length ?? 0) <= 200)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalEmail]]);
    }
}
