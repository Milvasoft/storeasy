using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.TestFlight.CreateBetaGroup;

/// <summary>
/// Validations of <see cref="CreateBetaGroupCommand"/>.
/// </summary>
public sealed class CreateBetaGroupCommandValidator : AbstractValidator<CreateBetaGroupCommand>
{
    ///<inheritdoc cref="CreateBetaGroupCommandValidator"/>
    public CreateBetaGroupCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(command => command.PublicLinkLimit)
            .InclusiveBetween(1, 10000)
            .When(command => command.PublicLinkLimit.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBetaGroup]]);
    }
}
