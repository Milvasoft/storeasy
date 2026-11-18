using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Studio;
using System.Linq.Expressions;

namespace Storeasy.Application.Behaviours;

/// <summary>
/// Validation rules shared by the mockup studio requests (template name, locales, design).
/// </summary>
public static class MockupValidationExtensions
{
    /// <summary>
    /// Template name: required, at most <see cref="MockupTemplate.NameMaxLength"/> characters.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="name"></param>
    /// <param name="localizer"></param>
    public static void RuleForMockupName<T>(this AbstractValidator<T> validator, Expression<Func<T, string>> name, IMilvaLocalizer localizer)
    {
        validator.RuleFor(name)
                 .NotNullOrEmpty(localizer, MessageKey.GlobalName)
                 .MaximumLength(MockupTemplate.NameMaxLength)
                 .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], MockupTemplate.NameMaxLength]);
    }

    /// <summary>
    /// Locales: one to <see cref="MockupTemplate.MaxLocaleCount"/> distinct language tags, the primary one among them.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="locales"></param>
    /// <param name="primaryLocale"></param>
    /// <param name="localizer"></param>
    public static void RuleForMockupLocales<T>(this AbstractValidator<T> validator, Func<T, List<string>> locales, Func<T, string> primaryLocale, IMilvaLocalizer localizer)
    {
        validator.RuleFor(request => locales(request))
                 .Must(list => list != null && list.Count is > 0 and <= MockupTemplate.MaxLocaleCount && list.All(MockupSpecRules.IsLocale) && list.Distinct(StringComparer.OrdinalIgnoreCase).Count() == list.Count)
                 .WithMessage(request => localizer[MessageKey.MockupLocaleInvalid, string.Join(", ", locales(request) ?? [])])
                 .OverridePropertyName("Locales");

        validator.RuleFor(request => primaryLocale(request))
                 .Must((request, primary) => primary != null && (locales(request) ?? []).Contains(primary, StringComparer.OrdinalIgnoreCase))
                 .WithMessage(request => localizer[MessageKey.MockupLocaleInvalid, primaryLocale(request) ?? string.Empty])
                 .OverridePropertyName("PrimaryLocale");
    }

    /// <summary>
    /// Export selection: optional locales (language tags), optional class keys of the studio, optional size targets of
    /// known classes with positive sizes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="locales"></param>
    /// <param name="classKeys"></param>
    /// <param name="targets"></param>
    /// <param name="localizer"></param>
    public static void RuleForMockupExportSelection<T>(this AbstractValidator<T> validator,
                                                       Func<T, List<string>> locales,
                                                       Func<T, List<string>> classKeys,
                                                       Func<T, List<Dtos.StoreJobDtos.MockupExportTarget>> targets,
                                                       IMilvaLocalizer localizer)
    {
        validator.RuleFor(request => locales(request))
                 .Must(list => list == null || (list.Count > 0 && list.All(MockupSpecRules.IsLocale)))
                 .WithMessage(request => localizer[MessageKey.MockupLocaleInvalid, string.Join(", ", locales(request) ?? [])])
                 .OverridePropertyName("Locales");

        validator.RuleFor(request => classKeys(request))
                 .Must(list => list == null || list.All(StudioClassMap.IsKnown))
                 .WithMessage(request => localizer[MessageKey.MockupClassUnknown, string.Join(", ", (classKeys(request) ?? []).Where(k => !StudioClassMap.IsKnown(k)))])
                 .OverridePropertyName("ClassKeys");

        validator.RuleFor(request => targets(request))
                 .Must(list => list == null || list.All(t => t != null && StudioClassMap.IsKnown(t.ClassKey) && t.Width is > 0 and <= 8000 && t.Height is > 0 and <= 8000))
                 .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalScreenshotClass]])
                 .OverridePropertyName("Targets");
    }

    /// <summary>
    /// Design: every problem of <see cref="MockupSpecRules.Validate"/> becomes one validation error. A null design passes
    /// unless <paramref name="required"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="spec"></param>
    /// <param name="localizer"></param>
    /// <param name="required"></param>
    public static void RuleForMockupSpec<T>(this AbstractValidator<T> validator, Func<T, MockupSpec> spec, IMilvaLocalizer localizer, bool required)
    {
        validator.RuleFor(request => spec(request))
                 .Custom((value, context) =>
                 {
                     if (value == null && !required)
                         return;

                     foreach (var error in MockupSpecRules.Validate(value))
                         context.AddFailure("Spec", localizer[MessageKey.MockupSpecInvalid, error]);
                 });
    }
}
