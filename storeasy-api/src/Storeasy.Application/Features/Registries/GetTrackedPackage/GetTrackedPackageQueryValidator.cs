using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetTrackedPackage;

/// <summary>
/// Validations of <see cref="GetTrackedPackageQuery"/>.
/// </summary>
public sealed class GetTrackedPackageQueryValidator : AbstractValidator<GetTrackedPackageQuery>
{
    ///<inheritdoc cref="GetTrackedPackageQueryValidator"/>
    public GetTrackedPackageQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
