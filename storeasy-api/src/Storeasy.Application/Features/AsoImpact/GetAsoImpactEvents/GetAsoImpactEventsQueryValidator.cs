using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpactEvents;

/// <summary>
/// Validations of <see cref="GetAsoImpactEventsQuery"/>.
/// </summary>
public sealed class GetAsoImpactEventsQueryValidator : AbstractValidator<GetAsoImpactEventsQuery>
{
    ///<inheritdoc cref="GetAsoImpactEventsQueryValidator"/>
    public GetAsoImpactEventsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

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
