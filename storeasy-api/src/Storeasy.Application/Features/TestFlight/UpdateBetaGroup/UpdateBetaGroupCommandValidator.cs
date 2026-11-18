using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroup;

/// <summary>
/// Validations of <see cref="UpdateBetaGroupCommand"/>.
/// </summary>
public sealed class UpdateBetaGroupCommandValidator : AbstractValidator<UpdateBetaGroupCommand>
{
    ///<inheritdoc cref="UpdateBetaGroupCommandValidator"/>
    public UpdateBetaGroupCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .MaximumLength(200)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(command => command.PublicLinkLimit)
            .InclusiveBetween(1, 10000)
            .When(command => command.PublicLinkLimit.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBetaGroup]]);
    }
}
