using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetPackageSetup;

/// <summary>
/// Validations of <see cref="GetPackageSetupQuery"/>.
/// </summary>
public sealed class GetPackageSetupQueryValidator : AbstractValidator<GetPackageSetupQuery>
{
    ///<inheritdoc cref="GetPackageSetupQueryValidator"/>
    public GetPackageSetupQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(query => query.Provider)
            .IsInEnum()
            .When(query => query.Provider.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalCiProvider]]);
    }
}
