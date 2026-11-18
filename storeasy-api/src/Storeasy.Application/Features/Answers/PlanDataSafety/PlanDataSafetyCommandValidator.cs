using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.PlanDataSafety;

/// <summary>
/// Validations of <see cref="PlanDataSafetyCommand"/>.
/// </summary>
public sealed class PlanDataSafetyCommandValidator : AbstractValidator<PlanDataSafetyCommand>
{
    ///<inheritdoc cref="PlanDataSafetyCommandValidator"/>
    public PlanDataSafetyCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
