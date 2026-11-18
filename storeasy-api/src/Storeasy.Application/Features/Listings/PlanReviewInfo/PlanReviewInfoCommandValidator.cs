using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Listings.PlanReviewInfo;

/// <summary>
/// Validations of <see cref="PlanReviewInfoCommand"/>.
/// </summary>
public sealed class PlanReviewInfoCommandValidator : AbstractValidator<PlanReviewInfoCommand>
{
    ///<inheritdoc cref="PlanReviewInfoCommandValidator"/>
    public PlanReviewInfoCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.ContactEmail))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalEmail]]);
    }
}
