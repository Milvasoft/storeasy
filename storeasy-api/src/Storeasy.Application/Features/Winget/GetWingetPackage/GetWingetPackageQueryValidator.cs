using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Winget.GetWingetPackage;

/// <summary>
/// Validations of <see cref="GetWingetPackageQuery"/>.
/// </summary>
public sealed class GetWingetPackageQueryValidator : AbstractValidator<GetWingetPackageQuery>
{
    ///<inheritdoc cref="GetWingetPackageQueryValidator"/>
    public GetWingetPackageQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
