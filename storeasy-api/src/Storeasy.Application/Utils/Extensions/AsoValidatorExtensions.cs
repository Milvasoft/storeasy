using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Validation rules shared by the ASO keyword requests.
/// </summary>
public static class AsoValidatorExtensions
{
    /// <summary>
    /// The country must be two letters (ISO 3166-1 alpha-2).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="country"></param>
    /// <param name="localizer"></param>
    public static void AsoCountryRule<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string>> country, IMilvaLocalizer localizer)
    {
        validator.RuleFor(country)
                 .Must(c => AsoCountries.Normalize(c) != null)
                 .WithMessage(localizer[MessageKey.AsoCountryInvalid]);
    }

    /// <summary>
    /// Between one and <paramref name="max"/> keywords, none empty or longer than the keyword limit.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="keywords"></param>
    /// <param name="max"></param>
    /// <param name="localizer"></param>
    public static void AsoKeywordsRule<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, List<string>>> keywords, int max, IMilvaLocalizer localizer)
    {
        validator.RuleFor(keywords)
                 .Must(list => list is { Count: > 0 } && list.Count <= max && list.All(k => !string.IsNullOrWhiteSpace(k) && k.Trim().Length <= AsoTrackedKeyword.KeywordMaxLength))
                 .WithMessage(localizer[MessageKey.AsoKeywordsInvalid, max, AsoTrackedKeyword.KeywordMaxLength]);
    }
}
