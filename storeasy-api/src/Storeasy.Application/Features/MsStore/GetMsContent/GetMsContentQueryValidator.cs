using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.GetMsContent;

/// <summary>
/// Validations of <see cref="GetMsContentQuery"/>.
/// </summary>
public sealed class GetMsContentQueryValidator : AbstractValidator<GetMsContentQuery>
{
    ///<inheritdoc cref="GetMsContentQueryValidator"/>
    public GetMsContentQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(query => query.FlightId)
            .MaximumLength(MsProduct.IdMaxLength)
            .WithMessage(localizer[MessageKey.MsFlightNotFound]);

        RuleFor(query => query.AddonStoreId)
            .MaximumLength(MsProduct.IdMaxLength)
            .WithMessage(localizer[MessageKey.MsAddonNotFound]);
    }
}
