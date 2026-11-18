using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.AddGoogleApp;

/// <summary>
/// Validations of <see cref="AddGoogleAppCommand"/>.
/// </summary>
public sealed class AddGoogleAppCommandValidator : AbstractValidator<AddGoogleAppCommand>
{
    ///<inheritdoc cref="AddGoogleAppCommandValidator"/>
    public AddGoogleAppCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.PackageName)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PackageNameInvalid])
            .Matches("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+$")
            .WithMessage(localizer[MessageKey.PackageNameInvalid])
            .MaximumLength(StoreApp.ExternalIdMaxLength)
            .WithMessage(localizer[MessageKey.PackageNameInvalid]);
    }
}
