using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;

namespace Storeasy.Application.Features.Releases.SubmitForReview;

/// <summary>
/// Validations of <see cref="SubmitForReviewCommand"/>.
/// </summary>
public sealed class SubmitForReviewCommandValidator : AbstractValidator<SubmitForReviewCommand>
{
    ///<inheritdoc cref="SubmitForReviewCommandValidator"/>
    public SubmitForReviewCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Confirm)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PlanConfirmMismatch]);
    }
}
