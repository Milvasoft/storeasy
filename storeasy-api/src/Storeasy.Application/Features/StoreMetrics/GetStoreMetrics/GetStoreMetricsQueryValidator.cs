using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetrics;

/// <summary>
/// Validations of <see cref="GetStoreMetricsQuery"/>.
/// </summary>
public sealed class GetStoreMetricsQueryValidator : AbstractValidator<GetStoreMetricsQuery>
{
    ///<inheritdoc cref="GetStoreMetricsQueryValidator"/>
    public GetStoreMetricsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(query => query.Days)
            .InclusiveBetween(1, StoreMetricRules.MaxRangeDays)
            .When(query => query.Days.HasValue)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);

        RuleFor(query => query.Country)
            .MaximumLength(StoreMetricRules.CountryMaxLength)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);

        RuleFor(query => query.Source)
            .IsInEnum()
            .When(query => query.Source.HasValue)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);

        RuleFor(query => query.Store)
            .IsInEnum()
            .When(query => query.Store.HasValue)
            .WithMessage(localizer[MessageKey.StoreNotSupported]);
    }
}
