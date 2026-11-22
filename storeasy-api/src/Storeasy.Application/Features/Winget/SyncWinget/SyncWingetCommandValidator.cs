using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Winget.SyncWinget;

/// <summary>
/// Validations of <see cref="SyncWingetCommand"/>.
/// </summary>
public sealed class SyncWingetCommandValidator : AbstractValidator<SyncWingetCommand>
{
    ///<inheritdoc cref="SyncWingetCommandValidator"/>
    public SyncWingetCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
