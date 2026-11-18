using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.PurchaseSettings.PlanServerNotifications;

/// <summary>
/// Validations of <see cref="PlanServerNotificationsCommand"/>.
/// </summary>
public sealed class PlanServerNotificationsCommandValidator : AbstractValidator<PlanServerNotificationsCommand>
{
    ///<inheritdoc cref="PlanServerNotificationsCommandValidator"/>
    public PlanServerNotificationsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x)
            .Must(x => x.Url != null || x.SandboxUrl != null)
            .WithMessage(localizer[MessageKey.PlanNoChanges]);
    }
}
