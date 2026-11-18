using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.GetExpiringMsConnectionList;

/// <summary>
/// Validations of <see cref="GetExpiringMsConnectionListQuery"/>.
/// </summary>
public sealed class GetExpiringMsConnectionListQueryValidator : AbstractValidator<GetExpiringMsConnectionListQuery>
{
    ///<inheritdoc cref="GetExpiringMsConnectionListQueryValidator"/>
    public GetExpiringMsConnectionListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.Days)
            .InclusiveBetween(0, 800)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, "days"]);
    }
}
