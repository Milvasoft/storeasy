using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Firefox.GetFirefoxAddon;

/// <summary>
/// Validations of <see cref="GetFirefoxAddonQuery"/>.
/// </summary>
public sealed class GetFirefoxAddonQueryValidator : AbstractValidator<GetFirefoxAddonQuery>
{
    ///<inheritdoc cref="GetFirefoxAddonQueryValidator"/>
    public GetFirefoxAddonQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
