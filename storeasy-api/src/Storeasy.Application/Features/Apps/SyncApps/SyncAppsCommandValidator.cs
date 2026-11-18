using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.SyncApps;

/// <summary>
/// Validations of <see cref="SyncAppsCommand"/>.
/// </summary>
public sealed class SyncAppsCommandValidator : AbstractValidator<SyncAppsCommand>
{
    ///<inheritdoc cref="SyncAppsCommandValidator"/>
    public SyncAppsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ConnectionId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]])
            .When(command => command.ConnectionId.HasValue);
    }
}
