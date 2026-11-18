using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionText;

/// <summary>
/// Validations of <see cref="UpdateRejectionTextCommand"/>.
/// </summary>
public sealed class UpdateRejectionTextCommandValidator : AbstractValidator<UpdateRejectionTextCommand>
{
    ///<inheritdoc cref="UpdateRejectionTextCommandValidator"/>
    public UpdateRejectionTextCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.RawText)
            .NotEmpty()
            .MaximumLength(RejectionCase.RawTextMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
