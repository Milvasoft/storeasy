using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.GetMsProductDetail;

/// <summary>
/// Validations of <see cref="GetMsProductDetailQuery"/>.
/// </summary>
public sealed class GetMsProductDetailQueryValidator : AbstractValidator<GetMsProductDetailQuery>
{
    ///<inheritdoc cref="GetMsProductDetailQueryValidator"/>
    public GetMsProductDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
