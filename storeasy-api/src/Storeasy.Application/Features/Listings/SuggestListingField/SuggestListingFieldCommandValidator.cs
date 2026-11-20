using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Listings.SuggestListingField;

/// <summary>
/// Validations of <see cref="SuggestListingFieldCommand"/>. Whether the field gets suggestions in its store is checked by
/// the handler, which knows the store.
/// </summary>
public sealed class SuggestListingFieldCommandValidator : AbstractValidator<SuggestListingFieldCommand>
{
    ///<inheritdoc cref="SuggestListingFieldCommandValidator"/>
    public SuggestListingFieldCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Locale)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalLocale]]);

        RuleFor(x => x.Field)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalField]]);

        RuleFor(x => x.Instruction)
            .MaximumLength(SuggestListingFieldCommand.InstructionMaxLength)
            .WithMessage(localizer[MessageKey.AiSuggestionNoteTooLong, SuggestListingFieldCommand.InstructionMaxLength]);
    }
}
