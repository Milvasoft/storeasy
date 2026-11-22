using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Firefox.SyncFirefox;

/// <summary>
/// Validations of <see cref="SyncFirefoxCommand"/>.
/// </summary>
public sealed class SyncFirefoxCommandValidator : AbstractValidator<SyncFirefoxCommand>
{
    /// <summary>
    ///<inheritdoc cref="SyncFirefoxCommandValidator"/>
    public SyncFirefoxCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
