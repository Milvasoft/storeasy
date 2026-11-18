using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionReplyDraft;

/// <summary>
/// Validations of <see cref="UpdateRejectionReplyDraftCommand"/>.
/// </summary>
public sealed class UpdateRejectionReplyDraftCommandValidator : AbstractValidator<UpdateRejectionReplyDraftCommand>
{
    ///<inheritdoc cref="UpdateRejectionReplyDraftCommandValidator"/>
    public UpdateRejectionReplyDraftCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Body)
            .MaximumLength(RejectionCase.DraftMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);
    }
}
