using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.StoreConnections.VerifyStoreConnection;

/// <summary>
/// Validations of <see cref="VerifyStoreConnectionCommand"/>.
/// </summary>
public sealed class VerifyStoreConnectionCommandValidator : AbstractValidator<VerifyStoreConnectionCommand>
{
    ///<inheritdoc cref="VerifyStoreConnectionCommandValidator"/>
    public VerifyStoreConnectionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.PackageName)
            .Matches("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+$")
            .MaximumLength(150)
            .WithMessage(localizer[MessageKey.PackageNameInvalid])
            .When(command => !string.IsNullOrEmpty(command.PackageName));
    }
}
