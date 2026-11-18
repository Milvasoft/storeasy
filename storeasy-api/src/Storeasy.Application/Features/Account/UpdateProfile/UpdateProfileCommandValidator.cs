using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Core.MultiLanguage.Manager;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Account.UpdateProfile;

/// <summary>
/// Validations of <see cref="UpdateProfileCommand"/>.
/// </summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    ///<inheritdoc cref="UpdateProfileCommandValidator"/>
    public UpdateProfileCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotNullOrEmpty(localizer, MessageKey.GlobalName)
            .MaximumLength(User.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], User.NameMaxLength]);

        RuleFor(command => command.Surname)
            .MaximumLength(User.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalSurname], User.NameMaxLength]);

        RuleFor(command => command.Language)
            .Must(language => LanguagesSeed.Seed.Any(l => string.Equals(l.Code, language, StringComparison.OrdinalIgnoreCase)))
            .WithMessage(localizer[MessageKey.LanguageNotSupported])
            .When(command => !string.IsNullOrWhiteSpace(command.Language));
    }
}
