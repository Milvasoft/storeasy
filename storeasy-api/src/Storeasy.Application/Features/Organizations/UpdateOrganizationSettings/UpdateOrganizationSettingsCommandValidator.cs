using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Organizations;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Features.Organizations.UpdateOrganizationSettings;

/// <summary>
/// Validations of <see cref="UpdateOrganizationSettingsCommand"/>.
/// </summary>
public sealed partial class UpdateOrganizationSettingsCommandValidator : AbstractValidator<UpdateOrganizationSettingsCommand>
{
    ///<inheritdoc cref="UpdateOrganizationSettingsCommandValidator"/>
    public UpdateOrganizationSettingsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AuditRetentionDays)
            .Must(days => Organization.AuditRetentionChoices.Contains(days.Value))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRetention]])
            .When(command => command.AuditRetentionDays.HasValue);

        RuleFor(command => command.DefaultLocale)
            .Must(locale => LocalePattern().IsMatch(locale))
            .WithMessage(localizer[MessageKey.LanguageNotSupported])
            .When(command => !string.IsNullOrEmpty(command.DefaultLocale));
    }

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2}$")]
    private static partial Regex LocalePattern();
}
