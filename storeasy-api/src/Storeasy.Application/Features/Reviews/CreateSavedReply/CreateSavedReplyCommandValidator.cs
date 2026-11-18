using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.CreateSavedReply;

/// <summary>
/// Validations of <see cref="CreateSavedReplyCommand"/>.
/// </summary>
public sealed class CreateSavedReplyCommandValidator : AbstractValidator<CreateSavedReplyCommand>
{
    ///<inheritdoc cref="CreateSavedReplyCommandValidator"/>
    public CreateSavedReplyCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(SavedReply.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(command => command.Variants)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleForEach(command => command.Variants)
            .Must(SavedReplies.IsValidVariant)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleForEach(command => command.Variants)
            .Must(v => v == null || SavedReplies.UnknownVariables(v.Text).Count == 0)
            .WithMessage(localizer[MessageKey.SavedReplyVariableUnknown]);
    }
}
