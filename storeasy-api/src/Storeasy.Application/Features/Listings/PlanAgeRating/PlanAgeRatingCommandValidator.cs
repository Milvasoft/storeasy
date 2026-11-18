using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanAgeRating;

/// <summary>
/// Validations of <see cref="PlanAgeRatingCommand"/>.
/// </summary>
public sealed class PlanAgeRatingCommandValidator : AbstractValidator<PlanAgeRatingCommand>
{
    ///<inheritdoc cref="PlanAgeRatingCommandValidator"/>
    public PlanAgeRatingCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Answers)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.CannotBeEmpty, localizer[MessageKey.GlobalAnswers]]);
    }
}
