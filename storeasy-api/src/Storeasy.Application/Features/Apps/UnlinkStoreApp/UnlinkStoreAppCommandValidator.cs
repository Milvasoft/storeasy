using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.UnlinkStoreApp;

/// <summary>
/// Validations of <see cref="UnlinkStoreAppCommand"/>.
/// </summary>
public sealed class UnlinkStoreAppCommandValidator : AbstractValidator<UnlinkStoreAppCommand>
{
    ///<inheritdoc cref="UnlinkStoreAppCommandValidator"/>
    public UnlinkStoreAppCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
