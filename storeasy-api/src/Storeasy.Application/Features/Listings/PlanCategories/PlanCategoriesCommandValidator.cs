using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanCategories;

/// <summary>
/// Validations of <see cref="PlanCategoriesCommand"/>.
/// </summary>
public sealed class PlanCategoriesCommandValidator : AbstractValidator<PlanCategoriesCommand>
{
    ///<inheritdoc cref="PlanCategoriesCommandValidator"/>
    public PlanCategoriesCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Secondary)
            .NotEqual(x => x.Primary)
            .When(x => !string.IsNullOrEmpty(x.Secondary) && x.Primary != null)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalCategory]]);

        RuleFor(x => x.ContentRightsDeclaration)
            .Must(c => c == null || c is "DOES_NOT_USE_THIRD_PARTY_CONTENT" or "USES_THIRD_PARTY_CONTENT")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalValue]]);
    }
}
