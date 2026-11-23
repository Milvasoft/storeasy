using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.GetTrackedPackageList;

/// <summary>
/// Validations of <see cref="GetTrackedPackageListQuery"/>.
/// </summary>
public sealed class GetTrackedPackageListQueryValidator : AbstractValidator<GetTrackedPackageListQuery>
{
    ///<inheritdoc cref="GetTrackedPackageListQueryValidator"/>
    public GetTrackedPackageListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.Registry)
            .IsInEnum()
            .When(query => query.Registry.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPackageRegistry]]);

        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .When(query => query.AppId.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
