using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Chrome.SyncChrome;

/// <summary>
/// Validations of <see cref="SyncChromeCommand"/>.
/// </summary>
public sealed class SyncChromeCommandValidator : AbstractValidator<SyncChromeCommand>
{
    ///<inheritdoc cref="SyncChromeCommandValidator"/>
    public SyncChromeCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
