using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaLocalization;

/// <summary>
/// Validations of <see cref="UpdateBetaLocalizationCommand"/>.
/// </summary>
public sealed class UpdateBetaLocalizationCommandValidator : AbstractValidator<UpdateBetaLocalizationCommand>
{
    ///<inheritdoc cref="UpdateBetaLocalizationCommandValidator"/>
    public UpdateBetaLocalizationCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Locale)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalLocale]]);

        RuleFor(command => command.Description)
            .MaximumLength(BetaAppLocalization.DescriptionMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalText]]);

        RuleFor(command => command.FeedbackEmail)
            .MaximumLength(200)
            .Must(email => string.IsNullOrWhiteSpace(email) || email.Contains('@'))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalEmail]]);

        RuleFor(command => command.MarketingUrl)
            .MaximumLength(500)
            .Must(BeHttpsOrEmpty)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalUrl]]);

        RuleFor(command => command.PrivacyPolicyUrl)
            .MaximumLength(500)
            .Must(BeHttpsOrEmpty)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalUrl]]);
    }

    private static bool BeHttpsOrEmpty(string url) => string.IsNullOrWhiteSpace(url) || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
