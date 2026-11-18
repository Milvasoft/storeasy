using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.LinkStoreApp;

/// <summary>
/// Validations of <see cref="LinkStoreAppCommand"/>.
/// </summary>
public sealed class LinkStoreAppCommandValidator : AbstractValidator<LinkStoreAppCommand>
{
    ///<inheritdoc cref="LinkStoreAppCommandValidator"/>
    public LinkStoreAppCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
