using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetPackageListing;

/// <summary>
/// Validations of <see cref="GetPackageListingQuery"/>.
/// </summary>
public sealed class GetPackageListingQueryValidator : AbstractValidator<GetPackageListingQuery>
{
    ///<inheritdoc cref="GetPackageListingQueryValidator"/>
    public GetPackageListingQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
