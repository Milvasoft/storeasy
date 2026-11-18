using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Rejections.CloseRejectionCase;

/// <summary>
/// Validations of <see cref="CloseRejectionCaseCommand"/>.
/// </summary>
public sealed class CloseRejectionCaseCommandValidator : AbstractValidator<CloseRejectionCaseCommand>
{
    ///<inheritdoc cref="CloseRejectionCaseCommandValidator"/>
    public CloseRejectionCaseCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Note)
            .MaximumLength(500)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
