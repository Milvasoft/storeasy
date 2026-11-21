using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Winget.AddWingetPackage;

/// <summary>
/// Validations of <see cref="AddWingetPackageCommand"/>.
/// </summary>
public sealed class AddWingetPackageCommandValidator : AbstractValidator<AddWingetPackageCommand>
{
    ///<inheritdoc cref="AddWingetPackageCommandValidator"/>
    public AddWingetPackageCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.PackageIdentifier)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.WingetPackageIdentifierInvalid])
            .MaximumLength(128)
            .WithMessage(localizer[MessageKey.WingetPackageIdentifierInvalid])
            .Must(id => id != null && WingetFields.PackageIdentifier().IsMatch(id.Trim()))
            .WithMessage(localizer[MessageKey.WingetPackageIdentifierInvalid]);

        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .When(command => command.AppId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(command => command.Name)
            .MaximumLength(App.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], App.NameMaxLength]);
    }
}
