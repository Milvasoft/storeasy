using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.GetMsAddonList;

/// <summary>
/// Validations of <see cref="GetMsAddonListQuery"/>.
/// </summary>
public sealed class GetMsAddonListQueryValidator : AbstractValidator<GetMsAddonListQuery>
{
    ///<inheritdoc cref="GetMsAddonListQueryValidator"/>
    public GetMsAddonListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
