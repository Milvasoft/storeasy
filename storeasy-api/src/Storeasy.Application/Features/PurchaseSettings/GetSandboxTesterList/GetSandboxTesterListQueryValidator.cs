using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Features.PurchaseSettings.GetSandboxTesterList;

/// <summary>
/// Validations of <see cref="GetSandboxTesterListQuery"/>.
/// </summary>
public sealed class GetSandboxTesterListQueryValidator : AbstractValidator<GetSandboxTesterListQuery>
{
    ///<inheritdoc cref="GetSandboxTesterListQueryValidator"/>
    public GetSandboxTesterListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
