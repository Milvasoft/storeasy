using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetPackageDownloads;

/// <summary>
/// Validations of <see cref="GetPackageDownloadsQuery"/>.
/// </summary>
public sealed class GetPackageDownloadsQueryValidator : AbstractValidator<GetPackageDownloadsQuery>
{
    ///<inheritdoc cref="GetPackageDownloadsQueryValidator"/>
    public GetPackageDownloadsQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TrackedPackageId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);
    }
}
