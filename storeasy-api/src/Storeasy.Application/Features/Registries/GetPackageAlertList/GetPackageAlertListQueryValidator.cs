using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetPackageAlertList;

/// <summary>
/// Validations of <see cref="GetPackageAlertListQuery"/>.
/// </summary>
public sealed class GetPackageAlertListQueryValidator : AbstractValidator<GetPackageAlertListQuery>
{
    ///<inheritdoc cref="GetPackageAlertListQueryValidator"/>
    public GetPackageAlertListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TrackedPackageId)
            .GreaterThan(0)
            .When(query => query.TrackedPackageId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
