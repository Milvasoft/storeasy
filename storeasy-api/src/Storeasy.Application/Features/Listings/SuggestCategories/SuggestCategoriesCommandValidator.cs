using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Listings.SuggestCategories;

/// <summary>
/// Validations of <see cref="SuggestCategoriesCommand"/>.
/// </summary>
public sealed class SuggestCategoriesCommandValidator : AbstractValidator<SuggestCategoriesCommand>
{
    ///<inheritdoc cref="SuggestCategoriesCommandValidator"/>
    public SuggestCategoriesCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Instruction)
            .MaximumLength(SuggestCategoriesCommand.InstructionMaxLength)
            .WithMessage(localizer[MessageKey.AiSuggestionNoteTooLong, SuggestCategoriesCommand.InstructionMaxLength]);
    }
}
