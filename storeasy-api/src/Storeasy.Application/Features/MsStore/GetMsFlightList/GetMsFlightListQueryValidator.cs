using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.GetMsFlightList;

/// <summary>
/// Validations of <see cref="GetMsFlightListQuery"/>.
/// </summary>
public sealed class GetMsFlightListQueryValidator : AbstractValidator<GetMsFlightListQuery>
{
    ///<inheritdoc cref="GetMsFlightListQueryValidator"/>
    public GetMsFlightListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
