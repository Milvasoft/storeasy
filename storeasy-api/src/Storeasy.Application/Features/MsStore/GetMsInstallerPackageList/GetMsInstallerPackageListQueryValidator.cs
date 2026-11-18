using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.GetMsInstallerPackageList;

/// <summary>
/// Validations of <see cref="GetMsInstallerPackageListQuery"/>.
/// </summary>
public sealed class GetMsInstallerPackageListQueryValidator : AbstractValidator<GetMsInstallerPackageListQuery>
{
    ///<inheritdoc cref="GetMsInstallerPackageListQueryValidator"/>
    public GetMsInstallerPackageListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
