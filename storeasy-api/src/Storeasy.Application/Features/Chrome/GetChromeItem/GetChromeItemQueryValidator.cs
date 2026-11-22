using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Chrome.GetChromeItem;

/// <summary>
/// Validations of <see cref="GetChromeItemQuery"/>.
/// </summary>
public sealed class GetChromeItemQueryValidator : AbstractValidator<GetChromeItemQuery>
{
    ///<inheritdoc cref="GetChromeItemQueryValidator"/>
    public GetChromeItemQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
