using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpactRecent;

/// <summary>
/// Validations of <see cref="GetAsoImpactRecentQuery"/>.
/// </summary>
public sealed class GetAsoImpactRecentQueryValidator : AbstractValidator<GetAsoImpactRecentQuery>
{
    ///<inheritdoc cref="GetAsoImpactRecentQueryValidator"/>
    public GetAsoImpactRecentQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.Days)
            .InclusiveBetween(1, GetAsoImpactRecentQuery.MaxDays)
            .When(query => query.Days.HasValue)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);

        RuleFor(query => query.WindowDays)
            .InclusiveBetween(ImpactOptions.MinWindowDays, ImpactOptions.MaxWindowDays)
            .When(query => query.WindowDays.HasValue)
            .WithMessage(localizer[MessageKey.AsoImpactWindowInvalid]);

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, GetAsoImpactRecentQuery.MaxLimit)
            .When(query => query.Limit.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, "Limit"]);

        RuleFor(query => query.Store)
            .IsInEnum()
            .When(query => query.Store.HasValue)
            .WithMessage(localizer[MessageKey.StoreNotSupported]);
    }
}
