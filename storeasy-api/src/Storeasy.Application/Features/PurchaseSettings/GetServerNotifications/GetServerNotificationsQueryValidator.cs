using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Features.PurchaseSettings.GetServerNotifications;

/// <summary>
/// Validations of <see cref="GetServerNotificationsQuery"/>.
/// </summary>
public sealed class GetServerNotificationsQueryValidator : AbstractValidator<GetServerNotificationsQuery>
{
    ///<inheritdoc cref="GetServerNotificationsQueryValidator"/>
    public GetServerNotificationsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
