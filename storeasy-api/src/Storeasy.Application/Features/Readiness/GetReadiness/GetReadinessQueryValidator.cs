using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;

namespace Storeasy.Application.Features.Readiness.GetReadiness;

/// <summary>
/// Validations of <see cref="GetReadinessQuery"/>.
/// </summary>
public sealed class GetReadinessQueryValidator : AbstractValidator<GetReadinessQuery>
{
    ///<inheritdoc cref="GetReadinessQueryValidator"/>
    public GetReadinessQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.Store)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStore]]);
    }
}
