using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpact;

/// <summary>
/// Validations of <see cref="GetAsoImpactQuery"/>.
/// </summary>
public sealed class GetAsoImpactQueryValidator : AbstractValidator<GetAsoImpactQuery>
{
    /// <summary>
    /// Longest event id.
    /// </summary>
    public const int EventIdMaxLength = 100;

    ///<inheritdoc cref="GetAsoImpactQueryValidator"/>
    public GetAsoImpactQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(query => query.EventId)
            .NotEmpty()
            .MaximumLength(EventIdMaxLength)
            .WithMessage(localizer[MessageKey.AsoImpactEventNotFound]);

        RuleFor(query => query.WindowDays)
            .InclusiveBetween(ImpactOptions.MinWindowDays, ImpactOptions.MaxWindowDays)
            .When(query => query.WindowDays.HasValue)
            .WithMessage(localizer[MessageKey.AsoImpactWindowInvalid]);

        RuleFor(query => query.GapDays)
            .InclusiveBetween(0, ImpactOptions.MaxGapDays)
            .When(query => query.GapDays.HasValue)
            .WithMessage(localizer[MessageKey.AsoImpactWindowInvalid]);

        RuleFor(query => query.Country)
            .MaximumLength(StoreMetricRules.CountryMaxLength)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);

        RuleFor(query => query.Source)
            .IsInEnum()
            .When(query => query.Source.HasValue)
            .WithMessage(localizer[MessageKey.StoreMetricsRangeInvalid]);
    }
}
