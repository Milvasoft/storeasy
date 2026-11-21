using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetricsSummary;

/// <summary>
/// Validations of <see cref="GetStoreMetricsSummaryQuery"/>.
/// </summary>
public sealed class GetStoreMetricsSummaryQueryValidator : AbstractValidator<GetStoreMetricsSummaryQuery>
{
    ///<inheritdoc cref="GetStoreMetricsSummaryQueryValidator"/>
    public GetStoreMetricsSummaryQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.Days)
            .InclusiveBetween(1, StoreMetricRules.MaxRangeDays)
            .When(query => query.Days.HasValue)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);

        RuleFor(query => query.Store)
            .IsInEnum()
            .When(query => query.Store.HasValue)
            .WithMessage(localizer[MessageKey.StoreNotSupported]);
    }
}
