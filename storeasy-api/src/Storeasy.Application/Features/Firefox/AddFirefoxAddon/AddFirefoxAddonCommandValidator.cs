using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Firefox.AddFirefoxAddon;

/// <summary>
/// Validations of <see cref="AddFirefoxAddonCommand"/>.
/// </summary>
public sealed class AddFirefoxAddonCommandValidator : AbstractValidator<AddFirefoxAddonCommand>
{
    /// <summary>
    ///<inheritdoc cref="AddFirefoxAddonCommandValidator"/>
    public AddFirefoxAddonCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.Addon)
            .Must(addon => FirefoxVersions.IsAddonReference(addon?.Trim()))
            .WithMessage(localizer[MessageKey.FirefoxAddonInvalid]);

        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .When(command => command.AppId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(command => command.Name)
            .MaximumLength(App.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], App.NameMaxLength]);
    }
}
